using System.Globalization;
using FlightPrep.Models;
using FlightPrep.Models.Trajectory;
using Microsoft.Extensions.Logging;

namespace FlightPrep.Services;

public record HourlyForecast(DateTime Time, double TempC, double WindSpeedKmh, int WindDirDeg, int PrecipProb);

public class WeatherFetchService(IHttpClientFactory httpFactory, ILogger<WeatherFetchService> logger)
{
    public async Task<string?> FetchMetarAsync(string icao)
    {
        try
        {
            var client = httpFactory.CreateClient("aviationweather");
            var raw = await client.GetStringAsync($"api/data/metar?ids={Uri.EscapeDataString(icao)}&format=raw&hours=2");
            return raw?.Trim().Split('\n').FirstOrDefault(l => l.StartsWith(icao.ToUpper()))?.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FetchMetarAsync failed for {Icao}", icao);
            return null;
        }
    }

    public async Task<string?> FetchTafAsync(string icao)
    {
        try
        {
            var client = httpFactory.CreateClient("aviationweather");
            var raw = await client.GetStringAsync($"api/data/taf?ids={Uri.EscapeDataString(icao)}&format=raw");
            return raw?.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FetchTafAsync failed for {Icao}", icao);
            return null;
        }
    }

    public async Task<List<HourlyForecast>> FetchForecastAsync(double lat, double lon)
    {
        try
        {
            var client = httpFactory.CreateClient("openmeteo");
            var url = $"v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&hourly=temperature_2m,windspeed_10m,winddirection_10m,precipitation_probability&forecast_days=3&timezone=Europe%2FBrussels";
            var json = await client.GetStringAsync(url);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var hourly = doc.RootElement.GetProperty("hourly");
            var times = hourly.GetProperty("time").EnumerateArray().ToList();
            var temps = hourly.GetProperty("temperature_2m").EnumerateArray().ToList();
            var winds = hourly.GetProperty("windspeed_10m").EnumerateArray().ToList();
            var dirs = hourly.GetProperty("winddirection_10m").EnumerateArray().ToList();
            var precip = hourly.GetProperty("precipitation_probability").EnumerateArray().ToList();
            var result = new List<HourlyForecast>();
            for (int i = 0; i < times.Count; i++)
            {
                if (DateTime.TryParse(times[i].GetString(), out var dt))
                    result.Add(new HourlyForecast(dt, temps[i].GetDouble(), winds[i].GetDouble(), dirs[i].GetInt32(), precip[i].GetInt32()));
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FetchForecastAsync failed for {Lat},{Lon}", lat, lon);
            return new();
        }
    }

    public async Task<List<WindLevel>> FetchWindProfileAsync(double lat, double lon, DateTime flightDateTime)
    {
        // open-meteo pressure-level wind data
        // Pressure levels and their approximate altitudes
        var pressureLevels = new[]
        {
            (hPa: 1000, AltFt: 360),
            (hPa: 975,  AltFt: 1050),
            (hPa: 950,  AltFt: 1640),
            (hPa: 925,  AltFt: 2625),
            (hPa: 900,  AltFt: 3280),
            (hPa: 850,  AltFt: 4920),
            (hPa: 800,  AltFt: 6230),
            (hPa: 700,  AltFt: 9840),
        };

        // Build comma-separated hourly variable list
        var speedVars = string.Join(",", pressureLevels.Select(p => $"windspeed_{p.hPa}hPa"));
        var dirVars   = string.Join(",", pressureLevels.Select(p => $"winddirection_{p.hPa}hPa"));

        // Detect past date → use archive endpoint
        var today = DateTime.UtcNow.Date;
        string url;
        if (flightDateTime.Date < today)
        {
            var d = flightDateTime.ToString("yyyy-MM-dd");
            url = $"v1/archive?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&hourly={speedVars},{dirVars}&timezone=Europe%2FBrussels" +
                  $"&start_date={d}&end_date={d}";
        }
        else
        {
            url = $"v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&hourly={speedVars},{dirVars}&timezone=Europe%2FBrussels" +
                  $"&forecast_days=3";
        }

        try
        {
            var client = httpFactory.CreateClient("openmeteo");
            var json   = await client.GetStringAsync(url);
            var doc    = System.Text.Json.JsonDocument.Parse(json);
            var hourly = doc.RootElement.GetProperty("hourly");

            // Find the exact hour index; return empty on miss (avoids silent wrong-hour data)
            var times = hourly.GetProperty("time").EnumerateArray()
                              .Select((e, i) => (time: e.GetString(), idx: i)).ToList();
            var target = flightDateTime.ToString("yyyy-MM-ddTHH:00");
            int idx = times.FindIndex(t => t.time == target);
            if (idx < 0)
            {
                logger.LogWarning("FetchWindProfileAsync: target hour {Target} not found in time series for {Lat},{Lon}", target, lat, lon);
                return new();
            }

            var result = new List<WindLevel>();
            int order = 1;
            foreach (var (hPa, altFt) in pressureLevels)
            {
                var speedKmh = hourly.GetProperty($"windspeed_{hPa}hPa")[idx].GetDouble();
                var dirDeg   = hourly.GetProperty($"winddirection_{hPa}hPa")[idx].GetDouble();
                var speedKt  = (int)Math.Round(speedKmh / 1.852);
                result.Add(new WindLevel
                {
                    AltitudeFt   = altFt,
                    DirectionDeg = (int)Math.Round(dirDeg),
                    SpeedKt      = speedKt,
                    Order        = order++,
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FetchWindProfileAsync failed for {Lat},{Lon}", lat, lon);
            return new();
        }
    }

    public async Task<List<WindSnapshot>> FetchWindTimeSeriesAsync(
        double lat, double lon,
        DateTime fromUtc, DateTime toUtc)
    {
        var pressureLevels = new[]
        {
            (hPa: 1000, AltFt: 360),
            (hPa: 975,  AltFt: 1050),
            (hPa: 950,  AltFt: 1640),
            (hPa: 925,  AltFt: 2625),
            (hPa: 900,  AltFt: 3280),
            (hPa: 850,  AltFt: 4920),
            (hPa: 800,  AltFt: 6230),
            (hPa: 700,  AltFt: 9840),
        };

        var speedVars = string.Join(",", pressureLevels.Select(p => $"windspeed_{p.hPa}hPa"));
        var dirVars   = string.Join(",", pressureLevels.Select(p => $"winddirection_{p.hPa}hPa"));

        // Check if flight is beyond 3-day forecast window
        var maxForecastTime = DateTime.UtcNow.AddDays(3);
        if (fromUtc > maxForecastTime)
            return new();

        // Use archive endpoint for past flights, forecast for future
        var today = DateTime.UtcNow.Date;
        string url;
        if (fromUtc.Date < today)
        {
            var startDate = fromUtc.ToString("yyyy-MM-dd");
            var endDate   = toUtc.ToString("yyyy-MM-dd");
            url = $"v1/archive?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                  $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                  $"&hourly={speedVars},{dirVars}&timezone=UTC" +
                  $"&start_date={startDate}&end_date={endDate}";
        }
        else
        {
            url = $"v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                  $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                  $"&hourly={speedVars},{dirVars}&timezone=UTC&forecast_days=3";
        }

        try
        {
            var client = httpFactory.CreateClient("openmeteo");
            var json   = await client.GetStringAsync(url);
            var doc    = System.Text.Json.JsonDocument.Parse(json);
            var hourly = doc.RootElement.GetProperty("hourly");

            var timeArray = hourly.GetProperty("time").EnumerateArray().ToList();
            var snapshots = new List<WindSnapshot>();

            for (int i = 0; i < timeArray.Count; i++)
            {
                if (!DateTime.TryParse(timeArray[i].GetString(), out var hourTime))
                    continue;
                hourTime = DateTime.SpecifyKind(hourTime, DateTimeKind.Utc);
                // Only include hours within the requested window (with 1-hour buffer on each side)
                if (hourTime < fromUtc.AddHours(-1) || hourTime > toUtc.AddHours(1))
                    continue;

                var levels = new List<WindLevel>();
                int order = 1;
                foreach (var (hPa, altFt) in pressureLevels)
                {
                    var speedKmh = hourly.GetProperty($"windspeed_{hPa}hPa")[i].GetDouble();
                    var dirDeg   = hourly.GetProperty($"winddirection_{hPa}hPa")[i].GetDouble();
                    levels.Add(new WindLevel
                    {
                        AltitudeFt   = altFt,
                        DirectionDeg = (int)Math.Round(dirDeg),
                        SpeedKt      = (int)Math.Round(speedKmh / 1.852),
                        Order        = order++,
                    });
                }
                snapshots.Add(new WindSnapshot(hourTime, levels));
            }
            return snapshots;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FetchWindTimeSeriesAsync failed for {Lat},{Lon}", lat, lon);
            return new();
        }
    }
}
