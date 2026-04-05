namespace FlightPrep.Domain.Services;

public interface IWeatherService
{
    Task<(string? Metar, string? Taf)> FetchAsync(string icao);
}
