using FlightPrep.Domain.Models;
using FlightPrep.Domain.Models.Trajectory;
using FlightPrep.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Security.Claims;
using System.Text.Json;

namespace FlightPrep.Components.Pages;

public partial class FlightView:ComponentBase
{
       [Parameter] public int Id { get; set; }
    private FlightPreparation? _fp;
    private FlightAssessment? _assessment;
    private string? _userId;
    private FlightImage? _lightboxImg;

    private bool _showFlownModal;
    private string? _flownLandingNotes;
    private int? _flownDurationMinutes;
    private string? _flownRemarks;
    private double? _ofpFuelConsumptionL;
    private string? _ofpLandingLocation;
    private string _ofpVisibleDefects = "";
    private string? _ofpVisibleDefectsNotes;

    private (TimeOnly Sunrise, TimeOnly Sunset)? _sunriseSunset;

    private List<TrackPoint>? _trackPoints;
    private bool _showTrackUpload;
    private string? _kmlUploadError;
    private bool _mapInitialized;

    private List<SimulatedTrajectory>? _savedTrajectories;
    private List<SimulatedTrajectory>? _savedEnhancedTrajectories;
    private bool _savedCombinedMapRendered;

    private void OpenLightbox(FlightImage img)
    {
        _lightboxImg = img;
    }

    private void CloseLightbox()
    {
        _lightboxImg = null;
    }

    private void LoadExistingTrack()
    {
        if (_fp?.KmlTrack != null)
            _trackPoints = KmlSvc.ParseCoordinates(_fp.KmlTrack);
    }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _userId = userId;
        var isAdmin = authState.User.IsInRole("Admin");

        _fp = await FpSvc.GetByIdAsync(Id);

        if (_fp != null)
        {
            // Ownership check — redirect if the flight belongs to another user
            if (_fp.CreatedByUserId != null && _fp.CreatedByUserId != userId && !isAdmin)
            {
                Nav.NavigateTo("/flights");
                return;
            }

            // Compute assessment (TotaalGewicht, LiftVoldoende, GoNoGo via service)
            _assessment = await AssessmentSvc.ComputeAsync(_fp, userId);

            // Initialize flown modal pre-fill
            _flownLandingNotes = _fp.ActualLandingNotes;
            _flownDurationMinutes = _fp.ActualFlightDurationMinutes;
            _flownRemarks = _fp.ActualRemarks;
            _ofpFuelConsumptionL = _fp.FuelConsumptionL;
            _ofpLandingLocation = _fp.LandingLocationText;
            _ofpVisibleDefects = _fp.VisibleDefects.HasValue ? _fp.VisibleDefects.Value.ToString() : "";
            _ofpVisibleDefectsNotes = _fp.VisibleDefectsNotes;

            // Sunrise/sunset
            var loc = _fp.Location;
            if (loc?.Latitude.HasValue == true && loc.Longitude.HasValue)
                _sunriseSunset = SunriseSvc.Calculate(_fp.Datum, loc.Latitude!.Value, loc.Longitude!.Value);

            LoadExistingTrack();

            if (!string.IsNullOrWhiteSpace(_fp?.TrajectorySimulationJson))
            {
                try
                {
                    var all = JsonSerializer.Deserialize<List<SimulatedTrajectory>>(_fp.TrajectorySimulationJson);
                    if (all is { Count: > 0 })
                    {
                        _savedTrajectories = all.Where(t => t.DataSource != TrajectoryDataSource.Hysplit).ToList();
                        _savedEnhancedTrajectories = all.Where(t => t.DataSource == TrajectoryDataSource.Hysplit).ToList();
                        if (_savedTrajectories.Count == 0) _savedTrajectories = null;
                        if (_savedEnhancedTrajectories.Count == 0) _savedEnhancedTrajectories = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to deserialize TrajectorySimulationJson for flight {Id}", _fp?.Id);
                    _savedTrajectories = null;
                }
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_trackPoints is { Count: > 0 } && !_mapInitialized)
        {
            _mapInitialized = true;
            var pts = _trackPoints.Select(p => new[] { p.Lat, p.Lon, p.AltM }).ToArray();
            await Js.InvokeVoidAsync("initFlightMap", "flight-track-map", pts);
            await Js.InvokeVoidAsync("initAltitudeChart", "altitude-chart", pts);
        }

        var hasSavedAny = (_savedTrajectories?.Count ?? 0) > 0
                          || _savedEnhancedTrajectories?.Any(t => t.Points.Count > 0) == true;
        if (hasSavedAny && !_savedCombinedMapRendered
                        && _fp?.Location is { Latitude: not null, Longitude: not null })
        {
            _savedCombinedMapRendered = true;
            var drJs = (_savedTrajectories ?? [])
                .Select(t => new
                {
                    color = t.Color,
                    label = $"{t.AltitudeFt} ft (DR)",
                    durationMin = t.DurationMinutes,
                    hasAltitude = false,
                    points = t.Points.Select(p => new[] { p.Lat, p.Lon }).ToArray()
                });
            var enhJs = (_savedEnhancedTrajectories ?? [])
                .Where(t => t.Points.Count > 0)
                .Select(t => new
                {
                    color = t.Color,
                    label = $"{t.AltitudeFt} ft (3D)",
                    durationMin = t.DurationMinutes,
                    hasAltitude = true,
                    points = t.Points.Select(p => new[] { p.Lat, p.Lon, p.AltitudeFt ?? 0 }).ToArray()
                });
            await Js.InvokeVoidAsync("initCombinedTrajectoryMap",
                "saved-combined-map",
                _fp!.Location!.Latitude,
                _fp.Location.Longitude,
                drJs.Concat<object>(enhJs).ToArray());
        }
    }

    private async Task OnKmlUpload(InputFileChangeEventArgs e)
    {
        _kmlUploadError = null;
        try
        {
            await using var stream = e.File.OpenReadStream(5 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            var kml = await reader.ReadToEndAsync();
            var pts = KmlSvc.ParseCoordinates(kml);
            if (pts.Count == 0)
            {
                _kmlUploadError = "Geen geldige coördinaten gevonden in het KML bestand.";
                return;
            }

            await FpSvc.PatchKmlTrackAsync(_fp!.Id, kml);

            _fp.KmlTrack = kml;
            _trackPoints = pts;
            _mapInitialized = false;
            _showTrackUpload = false;
        }
        catch (Exception ex)
        {
            _kmlUploadError = $"Fout bij uploaden: {ex.Message}";
        }
    }

    private void OpenFlownModal()
    {
        _flownLandingNotes = _fp?.ActualLandingNotes;
        _flownDurationMinutes = _fp?.ActualFlightDurationMinutes;
        _flownRemarks = _fp?.ActualRemarks;
        _ofpFuelConsumptionL = _fp?.FuelConsumptionL;
        _ofpLandingLocation = _fp?.LandingLocationText;
        _ofpVisibleDefects = _fp?.VisibleDefects.HasValue == true ? _fp.VisibleDefects.Value.ToString() : "";
        _ofpVisibleDefectsNotes = _fp?.VisibleDefectsNotes;
        _showFlownModal = true;
    }

    private async Task SaveFlown()
    {
        if (_fp == null) return;

        var visibleDefects = bool.TryParse(_ofpVisibleDefects, out var b) ? b : (bool?)null;

        await FpSvc.PatchFlownAsync(
            _fp.Id, true,
            _flownLandingNotes, _flownDurationMinutes, _flownRemarks,
            _ofpFuelConsumptionL, _ofpLandingLocation, visibleDefects, _ofpVisibleDefectsNotes);

        _fp.IsFlown = true;
        _fp.ActualLandingNotes = _flownLandingNotes;
        _fp.ActualFlightDurationMinutes = _flownDurationMinutes;
        _fp.ActualRemarks = _flownRemarks;
        _fp.FuelConsumptionL = _ofpFuelConsumptionL;
        _fp.LandingLocationText = _ofpLandingLocation;
        _fp.VisibleDefects = visibleDefects;
        _fp.VisibleDefectsNotes = _ofpVisibleDefectsNotes;
        _showFlownModal = false;
    }

    private async Task DownloadPdf()
    {
        if (_fp == null) return;
        var pdfBytes = await PdfSvc.GenerateAsync(_fp, userId: _userId);
        var fileName = $"vaartvoorbereiding_{_fp.Datum:yyyy-MM-dd}_{_fp.Id}.pdf";
        await Js.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf",
            Convert.ToBase64String(pdfBytes));
    }

    private async Task DownloadOfp()
    {
        if (_fp == null) return;
        var settings = await OfpSettingsSvc.GetSettingsAsync(_userId);
        var pdfBytes = await PdfSvc.GenerateOfpAsync(_fp, settings.PassengerEquipmentWeightKg);
        var fileName = $"OFP_{_fp.Datum:yyyy-MM-dd}_{_fp.Id}.pdf";
        await Js.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf",
            Convert.ToBase64String(pdfBytes));
    }

}
