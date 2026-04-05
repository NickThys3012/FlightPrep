namespace FlightPrep.Domain.Services;

public interface IPowerLineService
{
    Task<string?> GetGeoJsonAsync(double south, double west, double north, double east);
}
