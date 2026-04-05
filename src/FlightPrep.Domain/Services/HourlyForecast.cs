namespace FlightPrep.Domain.Services;

/// <summary>A single hourly weather forecast entry.</summary>
public record HourlyForecast(DateTime Time, double TempC, double WindSpeedKmh, int WindDirDeg, int PrecipProb);
