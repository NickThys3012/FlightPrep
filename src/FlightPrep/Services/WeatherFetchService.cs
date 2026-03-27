using System.Globalization;
using FlightPrep.Models;

namespace FlightPrep.Services;

public record HourlyForecast(DateTime Time, double TempC, double WindSpeedKmh, int WindDirDeg, int PrecipProb);

public class WeatherFetchService(IHttpClientFactory httpFactory)
{
    public async Task<string?> FetchMetarAsync(string icao)
    {
        try
        {
            var client = httpFactory.CreateClient("aviationweather");
            var raw = await client.GetStringAsync($"api/data/metar?ids={Uri.EscapeDataString(icao)}&format=raw&hours=2");
            return raw?.Trim().Split('\n').FirstOrDefault(l => l.StartsWith(icao.ToUpper()))?.Trim();
        }
        catch { return null; }
    }

    public async Task<string?> FetchTafAsync(string icao)
    {
        try
        {
            var client = httpFactory.CreateClient("aviationweather");
            var raw = await client.GetStringAsync($"api/data/taf?ids={Uri.EscapeDataString(icao)}&format=raw");
            return raw?.Trim();
        }
        catch { return null; }
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
        catch { return new(); }
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

            // Find the closest hour index
            var times = hourly.GetProperty("time").EnumerateArray()
                              .Select((e, i) => (time: e.GetString(), idx: i)).ToList();
            var target = flightDateTime.ToString("yyyy-MM-ddTHH:00");
            int idx = times.FirstOrDefault(t => t.time == target).idx;
            // Fallback: nearest hour
            if (idx == 0 && times[0].time != target)
                idx = 0;

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
        catch
        {
            return new();
        }
    }
}
