using FlightPrep.Models;
using FlightPrep.Models.Trajectory;

namespace FlightPrep.Services;

public interface IWeatherFetchService
{
    Task<string?> FetchMetarAsync(string icao);
    Task<string?> FetchTafAsync(string icao);
    Task<List<HourlyForecast>> FetchForecastAsync(double lat, double lon);
    Task<List<WindLevel>> FetchWindProfileAsync(double lat, double lon, DateTime flightDateTime);
    Task<List<WindSnapshot>> FetchWindTimeSeriesAsync(double lat, double lon, DateTime fromUtc, DateTime toUtc);
}
