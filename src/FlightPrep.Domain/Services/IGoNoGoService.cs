using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

public interface IGoNoGoService
{
    Task<GoNoGoSettings> GetSettingsAsync(string? userId);
    Task SaveSettingsAsync(GoNoGoSettings s, string? userId);
    string Compute(FlightPreparation fp, GoNoGoSettings s);
    string Compute(double? windKt, double? visKm, double? capeJkg, GoNoGoSettings s);
}
