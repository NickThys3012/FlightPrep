using FlightPrep.Domain.Models;
using FlightPrep.Domain.Models.Trajectory;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace FlightPrep.Components.Pages;

public partial class FlightEdit(
    IFlightPreparationService fpSvc,
    AuthenticationStateProvider authStateProvider,
    NavigationManager nav,
    IPdfService pdfSvc,
    ISunriseService sunriseSvc,
    IJSRuntime js,
    ITrajectoryService trajectorySvc,
    ILogger<FlightEdit> logger,
    IEnhancedTrajectoryService enhancedTrajectorySvc,
    IGoNoGoService goNoGoSvc,
    UserManager<ApplicationUser> userManager)
    : ComponentBase
{
   [Parameter] public int? Id { get; set; }
     private FlightPreparation? _fp;
    private List<Balloon> _balloons = [];
    private List<Pilot> _pilots = [];
    private List<Location> _locations = [];
    private Pilot? _currentPilot;
    private Location? _currentLocation;
    private GoNoGoSettings _goNoGoSettings = new();

    // Images kept in memory; persisted on save
    private List<FlightImage> _meteoImages = [];
    private List<FlightImage> _trajectImages = [];

    private bool _saving;
    private string? _saveMessage;
    private bool _saveError;
    private string? _userId;

    private int _simulationDurationMinutes = 60;
    private List<SimulatedTrajectory>? _simulatedTrajectories;
    private string? _trajectoryError;

    private double _enhAscentRateMs   = 3.0;
    private readonly List<int> _enhCruiseAltsFt = [3000];
    private double _enhDescentRateMs  = 2.0;
    private List<SimulatedTrajectory>? _enhancedTrajectories;
    private bool   _showEnhancedPanel;
    private bool   _combinedMapRendered;
    private string? _enhancedError;
    private bool   _computingEnhanced;

    private void AddCruiseAlt()
    {
        if (_enhCruiseAltsFt.Count < 5)
            _enhCruiseAltsFt.Add((_enhCruiseAltsFt.Count > 0 ? _enhCruiseAltsFt[^1] : 2000) + 1000);
    }

    private (TimeOnly Sunrise, TimeOnly Sunset)? _sunriseSunset;

    private const string DefaultPaxBriefing =
        "<h2>Pax Briefing</h2>" +
        "<h3>Voor de vlucht – Inflation</h3><ul>" +
        "<li>Wat er staat te gebeuren tijdens inflation</li>" +
        "<li><strong>NIET ROKEN</strong></li>" +
        "<li>Oppassen voor de ventilator</li>" +
        "<li>Alle PAX's en familie wachten achter de snuit van de wagen</li>" +
        "<li>Wanneer de ballon recht staat roept de piloot om in te stappen (toon instapgat)</li>" +
        "<li>Veiligheid checks vermelden (parachute)</li></ul>" +
        "<h3>Tijdens de vaart</h3><ul>" +
        "<li>Enkel vasthouden aan de stokken / mand / lussen / frame gascilinders</li>" +
        "<li>Niets overboord gooien (afval, etc.)</li>" +
        "<li>Uitkijken voor hoogspanning en dieren (paarden/koeien) – melden aan piloot</li></ul>" +
        "<h3>Landingspositie oefenen (na ±30 min)</h3><ul>" +
        "<li>Met de <strong>rug in de vaarrichting</strong></li>" +
        "<li>Stevig vasthouden aan de lussen in de mand</li>" +
        "<li>Licht door de knieën buigen (schok opvangen, kruisbanden deblokkeren)</li>" +
        "<li><strong>Nooit handen uit de mand</strong> als we aan het slepen zijn</li></ul>" +
        "<h3>Voor de landing</h3><ul>" +
        "<li>Alles opbergen (GSM / camera / etc.)</li>" +
        "<li>Landingspositie aannemen</li>" +
        "<li>Pas uitstappen wanneer de piloot het zegt</li></ul>" +
        "<h3>Na de landing</h3><ul>" +
        "<li>Ballon opruimen</li>" +
        "<li>Iets drinken 🙂</li></ul>" +
        "<h3>Algemeen</h3><ul>" +
        "<li>Aangeraden: stevige platte schoenen</li>" +
        "<li>Vraag naar medische conditie waarvan de piloot op de hoogte moet zijn</li></ul>";

    // Quill is initialised lazily when the user opens section 6 — never on a hidden element
    private async Task InitQuillOnOpen()
    {
        if (_fp == null) return;
        await Task.Delay(400); // Bootstrap collapse animation is ~350 ms
        await js.InvokeVoidAsync("quillInit", "pax-briefing-editor", _fp.PaxBriefing ?? "");
    }

    private async Task ReadQuillContent()
    {
        try
        {
            // quillGetHtml returns null when Quill was never opened → keep existing value
            var html = await js.InvokeAsync<string?>("quillGetHtml", "pax-briefing-editor");
            if (html != null) _fp!.PaxBriefing = html;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "JS interop not available during pre-render — expected");
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _balloons  = await fpSvc.GetBalloonsAsync();
        _pilots    = await fpSvc.GetPilotsAsync();
        _locations = await fpSvc.GetLocationsAsync();

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _userId = userId;
        var isAdmin = authState.User.IsInRole("Admin");

        _goNoGoSettings = await goNoGoSvc.GetSettingsAsync(userId);

        if (Id.HasValue)
        {
            _fp = await fpSvc.GetByIdAsync(Id.Value);

            if (_fp == null) { nav.NavigateTo("/flights"); return; }

            // Ownership check — block access if flight belongs to another user
            if (_fp.CreatedByUserId != null && _fp.CreatedByUserId != userId && !isAdmin)
            {
                nav.NavigateTo("/flights");
                return;
            }

            _currentPilot  = _fp.Pilot;
            _meteoImages   = _fp.Images.Where(i => i.Section == "Meteo").ToList();
            _trajectImages = _fp.Images.Where(i => i.Section == "Traject").ToList();
        }
        else
        {
            _fp = new FlightPreparation { PaxBriefing = DefaultPaxBriefing };
            _fp.CreatedByUserId = userId;

            // Populate OFP snapshot fields from ApplicationUser for new flights
            if (userId != null)
            {
                var appUser = await userManager.FindByIdAsync(userId);
                if (appUser != null)
                {
                    _fp.OperatorName = appUser.OperatorName;
                    _fp.PicWeightKg  = appUser.WeightKg;
                }
            }
        }

        UpdateSunriseSunset();
        UpdateCurrentLocation();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        // Combined trajectory map: render once whenever either DR or 3D trajectories are available
        var hasDr       = _simulatedTrajectories != null && _simulatedTrajectories.Count > 0;
        var hasEnh      = _enhancedTrajectories  != null && _enhancedTrajectories.Any(t => t.Points.Count > 0);
        var hasLocation = _fp?.Location?.Latitude != null && _fp.Location.Longitude != null;

        if ((hasDr || hasEnh) && !_combinedMapRendered && hasLocation)
        {
            _combinedMapRendered = true;
            var drTrajsJs = (_simulatedTrajectories ?? new())
                .Select(t => new
                {
                    color       = t.Color,
                    label       = $"{t.AltitudeFt} ft (DR)",
                    durationMin = t.DurationMinutes,
                    hasAltitude = false,
                    points      = t.Points.Select(p => new[] { p.Lat, p.Lon }).ToArray()
                });
            var enhTrajsJs = (_enhancedTrajectories ?? new())
                .Where(t => t.Points.Count > 0)
                .Select(t => new
                {
                    color       = t.Color,
                    label       = $"{t.AltitudeFt} ft (3D)",
                    durationMin = t.DurationMinutes,
                    hasAltitude = true,
                    points      = t.Points.Select(p => new[] { p.Lat, p.Lon, p.AltitudeFt ?? 0 }).ToArray()
                });
            await js.InvokeVoidAsync("initCombinedTrajectoryMap",
                "combined-trajectory-map",
                _fp!.Location!.Latitude,
                _fp.Location.Longitude,
                drTrajsJs.Concat<object>(enhTrajsJs).ToArray());
        }
    }

    private async Task SimuleerTraject()
    {
        _trajectoryError = null;
        _simulatedTrajectories = null;
        _combinedMapRendered = false;

        if (_fp?.Location?.Latitude is null || _fp.Location.Longitude is null)
        {
            _trajectoryError = "Locatie heeft geen coördinaten. Voeg lat/lon toe via Instellingen > Locaties.";
            return;
        }
        var valid = _fp.WindLevels
            .Where(w => w.DirectionDeg.HasValue && w.SpeedKt is > 0)
            .OrderBy(w => w.AltitudeFt)
            .ToList();
        if (!valid.Any())
        {
            _trajectoryError = "Geen geldige windniveaus. Vul minstens één hoogte in met richting en snelheid.";
            return;
        }
        if (_simulationDurationMinutes is <= 0 or > 360)
        {
            _trajectoryError = "Simulatieduur moet tussen 1 en 360 minuten liggen.";
            return;
        }
        _simulatedTrajectories = trajectorySvc.Compute(
            _fp.Location.Latitude.Value,
            _fp.Location.Longitude.Value,
            valid,
            _simulationDurationMinutes);

        await AutoSaveSimulationAsync();
    }

    private void MergeSimulationsToFp()
    {
        if (_fp is null) return;
        var all = new List<SimulatedTrajectory>(_simulatedTrajectories ?? new());
        if (_enhancedTrajectories != null)
            all.AddRange(_enhancedTrajectories.Where(t => t.Points.Count > 0));
        if (all.Count > 0)
            _fp.TrajectorySimulationJson = System.Text.Json.JsonSerializer.Serialize(all);
    }

    private async Task AutoSaveSimulationAsync()
    {
        MergeSimulationsToFp();
        if (_fp is null || _fp.Id == 0) return; // new flight — will be saved when user saves the form
        await fpSvc.PatchTrajectoryJsonAsync(_fp.Id, _fp.TrajectorySimulationJson);
    }

    private async Task BerekEnhancedTraject()
    {
        _enhancedError        = null;
        _enhancedTrajectories = null;
        _combinedMapRendered  = false;

        if (_fp?.Location?.Latitude is null || _fp.Location.Longitude is null)
        {
            _enhancedError = "Locatie heeft geen coördinaten. Voeg lat/lon toe via Instellingen > Locaties.";
            return;
        }
        if (!_enhCruiseAltsFt.Any())
        {
            _enhancedError = "Voer minstens één kruishoogte in.";
            return;
        }
        if (_enhCruiseAltsFt.Any(a => a < 500 || a > 20000))
        {
            _enhancedError = "Kruishoogte(s) moeten tussen 500 en 20000 ft liggen.";
            return;
        }
        if (_enhAscentRateMs  is <= 0 or > 10) { _enhancedError = "Klimsnelheid moet tussen 0.5 en 10 m/s liggen."; return; }
        if (_enhDescentRateMs is <= 0 or > 10) { _enhancedError = "Daalsnelheid moet tussen 0.5 en 10 m/s liggen."; return; }
        if (_simulationDurationMinutes is <= 0 or > 360) { _enhancedError = "Vluchtduur moet tussen 1 en 360 minuten liggen."; return; }

        _computingEnhanced = true;
        StateHasChanged();
        try
        {
            var launchUtc = DateTime.SpecifyKind(_fp.Datum.ToDateTime(_fp.Tijdstip), DateTimeKind.Utc);
            _enhancedTrajectories = await enhancedTrajectorySvc.ComputeMultipleAsync(
                _fp.Location.Latitude.Value,
                _fp.Location.Longitude.Value,
                launchUtc,
                _enhAscentRateMs,
                _enhCruiseAltsFt,
                _enhDescentRateMs,
                _simulationDurationMinutes);

            if (_enhancedTrajectories.All(t => t.Points.Count == 0))
                _enhancedError = "Geen winddata beschikbaar voor dit tijdstip. Probeer een andere vliegdatum of gebruik dead-reckoning.";
            else
                await AutoSaveSimulationAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BerekEnhancedTraject failed for flight {Id}", _fp?.Id);
            _enhancedError = "Berekening mislukt. Probeer opnieuw.";
        }
        finally
        {
            _computingEnhanced = false;
        }
    }

    private void OnBalloonChanged()
    {
        if (_fp == null) return;
        var balloon = _balloons.FirstOrDefault(b => b.Id == _fp.BalloonId);
        if (balloon != null && _fp.EnvelopeWeightKg == null)
            _fp.EnvelopeWeightKg = balloon.EmptyWeightKg;

        // Populate OFP weight snapshots from balloon reference weights (only if currently null)
        if (balloon != null)
        {
            if (_fp.OFPEnvelopeWeightKg == null) _fp.OFPEnvelopeWeightKg = balloon.EnvelopeOnlyWeightKg;
            if (_fp.OFPBasketWeightKg   == null) _fp.OFPBasketWeightKg   = balloon.BasketWeightKg;
            if (_fp.OFPBurnerWeightKg   == null) _fp.OFPBurnerWeightKg   = balloon.BurnerWeightKg;
            if (_fp.CylindersWeightKg   == null) _fp.CylindersWeightKg   = balloon.CylindersWeightKg;
        }
    }

    private void OnPilotChanged()
    {
        if (_fp == null) return;
        _currentPilot = _pilots.FirstOrDefault(p => p.Id == _fp.PilotId);
    }

    private void OnLocationChanged()
    {
        if (_fp == null) return;
        var loc = _locations.FirstOrDefault(l => l.Id == _fp.LocationId);
        if (loc?.AirspaceNotes != null && string.IsNullOrWhiteSpace(_fp.Luchtruimstructuur))
            _fp.Luchtruimstructuur = loc.AirspaceNotes;
        UpdateCurrentLocation();
        UpdateSunriseSunset();
    }

    private void UpdateCurrentLocation()
    {
        _currentLocation = _locations.FirstOrDefault(l => l.Id == _fp?.LocationId);
    }

    private void UpdateSunriseSunset()
    {
        if (_fp == null) { _sunriseSunset = null; return; }
        var loc = _locations.FirstOrDefault(l => l.Id == _fp.LocationId);
        if (loc?.Latitude.HasValue == true && loc.Longitude.HasValue)
            _sunriseSunset = sunriseSvc.Calculate(_fp.Datum, loc.Latitude!.Value, loc.Longitude!.Value);
        else
            _sunriseSunset = null;
    }

    // ── Save ─────────────────────────────────────────────────────────────────
    private async Task<FlightPreparation?> SaveInternal()
    {
        if (_fp == null) return null;
        MergeSimulationsToFp(); // ensure any in-memory simulations are persisted with the record
        await ReadQuillContent(); // pull latest HTML from editor
        _saving = true; _saveMessage = null;
        try
        {
            _currentPilot = _pilots.FirstOrDefault(p => p.Id == _fp.PilotId);

            // Defensive: stamp CreatedByUserId at save time for new flights.
            // Covers edge cases where OnInitializedAsync ran before auth state was ready.
            if (_fp.Id == 0 && _fp.CreatedByUserId == null)
            {
                var authState = await authStateProvider.GetAuthenticationStateAsync();
                _fp.CreatedByUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            // Merge split image lists back onto the entity before persisting.
            _fp.Images = _meteoImages.Concat(_trajectImages).ToList();

            await fpSvc.SaveAsync(_fp);

            // Re-populate navigation props from local lists (service may have cleared them
            // temporarily during EF operations; they are restored by the service, but ensure
            // in-memory image state is consistent with what we merged above).
            _fp.Balloon  = _balloons.FirstOrDefault(b => b.Id == _fp.BalloonId);
            _fp.Pilot    = _pilots.FirstOrDefault(p => p.Id == _fp.PilotId);
            _fp.Location = _locations.FirstOrDefault(l => l.Id == _fp.LocationId);
            _fp.Images   = _meteoImages.Concat(_trajectImages).ToList();

            _saveMessage = "✅ Vaart opgeslagen!"; _saveError = false;
            return _fp;
        }
        catch (Exception ex) { _saveMessage = $"❌ Fout bij opslaan: {ex.Message}"; _saveError = true; return null; }
        finally { _saving = false; }
    }

    private async Task SaveFlight()
    {
        var saved = await SaveInternal();
        if (saved != null && Id == null)
        {
            Id = saved.Id;
            nav.NavigateTo($"/flights/{saved.Id}/edit", replace: true);
        }
    }

    private async Task SaveAndPdf()
    {
        var saved = await SaveInternal();
        if (saved == null) return;
        try
        {
            var pdfBytes = await pdfSvc.GenerateAsync(saved, userId: _userId);
            await js.InvokeVoidAsync("downloadFileFromBytes",
                $"vaartvoorbereiding_{saved.Datum:yyyy-MM-dd}_{saved.Id}.pdf",
                "application/pdf", Convert.ToBase64String(pdfBytes));
        }
        catch (Exception ex) { _saveMessage = $"❌ PDF fout: {ex.Message}"; _saveError = true; }
    }
}
