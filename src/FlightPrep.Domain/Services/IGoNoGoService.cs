using FlightPrep.Models;

namespace FlightPrep.Services;

public interface IGoNoGoService
{
    Task<GoNoGoSettings> GetSettingsAsync(string? userId);
    Task SaveSettingsAsync(GoNoGoSettings s, string? userId);
    string Compute(FlightPreparation fp, GoNoGoSettings s);
    string Compute(double? windKt, double? visKm, double? capeJkg, GoNoGoSettings s);
}
