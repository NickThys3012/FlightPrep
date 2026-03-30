namespace FlightPrep.Services;

public interface IWeatherService
{
    Task<(string? Metar, string? Taf)> FetchAsync(string icao);
}
