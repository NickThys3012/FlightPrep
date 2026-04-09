using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FlightPrep.Components;

public partial class FlightMeteoSection : ComponentBase
{
    /// <summary>The current flight being edited. Weather fields are mutated directly via this reference.</summary>
    [Parameter]
    public FlightPreparation Fp { get; set; } = null!;

    /// <summary>Currently selected location (used to resolve ICAO code and coordinates for weather fetching).</summary>
    [Parameter]
    public Location? CurrentLocation { get; set; }

    /// <summary>Meteo images list shared with the parent; mutations are applied in-place.</summary>
    [Parameter]
    public List<FlightImage> MeteoImages { get; set; } = null!;

    [Inject] private IWeatherFetchService WeatherFetchSvc { get; set; } = null!;
    [Inject] private ILogger<FlightMeteoSection> Logger { get; set; } = null!;

    // ── Internal UI state ───────────────────────────────────────────────────
    private bool _fetchingWeather;
    private string? _weatherError;
    private bool _weatherSuccess;
    private bool _fetchingForecast;
    private List<HourlyForecast> _forecast = [];
    private bool _fetchingWindProfile;
    private string? _windProfileError;

    private bool _initialized;
    private bool _showWindProfile;

    private bool ShowWindProfile
    {
        get => _showWindProfile;
        set
        {
            if (_showWindProfile == value) return;
            _showWindProfile = value;
            if (!value)
                Fp.WindLevels.Clear();
        }
    }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Fp);
        ArgumentNullException.ThrowIfNull(MeteoImages);

        // Initialize the wind-profile toggle at once when Fp first arrives from the parent.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _showWindProfile = Fp.WindLevels.Count > 0;
    }

    // ── Weather fetching ────────────────────────────────────────────────────

    private async Task FetchWeather()
    {
        if (CurrentLocation?.IcaoCode == null) return;
        _weatherError = null;
        _weatherSuccess = false;
        _fetchingWeather = true;
        try
        {
            var metar = await WeatherFetchSvc.FetchMetarAsync(CurrentLocation.IcaoCode);
            var taf = await WeatherFetchSvc.FetchTafAsync(CurrentLocation.IcaoCode);
            if (metar != null) Fp.Metar = metar;
            if (taf != null) Fp.Taf = taf;
            if (metar == null && taf == null)
                _weatherError = "Geen data gevonden voor " + CurrentLocation.IcaoCode;
            else
                _weatherSuccess = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "FetchWeather failed for ICAO {Icao}", CurrentLocation?.IcaoCode);
            _weatherError = ex.Message;
        }
        finally
        {
            _fetchingWeather = false;
        }
    }

    private async Task FetchForecast()
    {
        if (CurrentLocation?.Latitude == null || CurrentLocation.Longitude == null) return;
        _fetchingForecast = true;
        try
        {
            _forecast = await WeatherFetchSvc.FetchForecastAsync(CurrentLocation.Latitude.Value, CurrentLocation.Longitude.Value);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "FetchForecast failed for flight {Id} — non-critical background fetch", Fp.Id);
        }
        finally
        {
            _fetchingForecast = false;
        }
    }

    private async Task FetchWindProfile()
    {
        _windProfileError = null;
        _fetchingWindProfile = true;
        StateHasChanged();
        try
        {
            if (Fp.Location?.Latitude is null || Fp.Location.Longitude is null)
            {
                _windProfileError = "Locatie heeft geen coördinaten.";
                return;
            }

            var flightDt = Fp.Datum.ToDateTime(Fp.Tijdstip);
            var levels = await WeatherFetchSvc.FetchWindProfileAsync(
                Fp.Location.Latitude.Value, Fp.Location.Longitude.Value, flightDt);
            if (!levels.Any())
            {
                _windProfileError = "Windprofiel ophalen mislukt. Probeer opnieuw.";
                return;
            }

            Fp.WindLevels.Clear();
            foreach (var lvl in levels)
            {
                lvl.FlightPreparationId = Fp.Id;
                Fp.WindLevels.Add(lvl);
            }

            ShowWindProfile = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "FetchWindProfile failed for flight {Id}", Fp.Id);
            _windProfileError = "Windprofiel ophalen mislukt. Probeer opnieuw.";
        }
        finally
        {
            _fetchingWindProfile = false;
        }
    }

    private void OnWindProfileToggleChanged(ChangeEventArgs e)
    {
        var enabled = e.Value is true;
        if (enabled && Fp is { WindLevels.Count: 0 })
            PopulateDefaultWindLevels();
        ShowWindProfile = enabled;
    }

    private void PopulateDefaultWindLevels()
    {
        int[] altitudes = [0, 500, 1000, 1500, 2000, 3000, 5000];
        for (var i = 0; i < altitudes.Length; i++)
            Fp.WindLevels.Add(new WindLevel { AltitudeFt = altitudes[i], Order = i });
    }
}
