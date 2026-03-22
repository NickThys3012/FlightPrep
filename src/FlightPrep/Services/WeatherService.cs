namespace FlightPrep.Services;

public class WeatherService(HttpClient http)
{
    public async Task<(string? Metar, string? Taf)> FetchAsync(string icao)
    {
        try
        {
            var metarTask = http.GetStringAsync(
                $"https://aviationweather.gov/api/data/metar?ids={icao}&format=raw");
            var tafTask = http.GetStringAsync(
                $"https://aviationweather.gov/api/data/taf?ids={icao}&format=raw");

            await Task.WhenAll(metarTask, tafTask);

            var metar = (await metarTask).Trim();
            var taf = (await tafTask).Trim();

            return (
                string.IsNullOrWhiteSpace(metar) ? null : metar,
                string.IsNullOrWhiteSpace(taf) ? null : taf
            );
        }
        catch
        {
            return (null, null);
        }
    }
}
