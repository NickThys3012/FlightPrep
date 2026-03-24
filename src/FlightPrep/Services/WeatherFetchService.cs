using System.Globalization;

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
}
