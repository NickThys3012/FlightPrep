using FlightPrep.Models;

namespace FlightPrep.Services;

public interface IGoNoGoService
{
    Task<GoNoGoSettings> GetSettingsAsync();
    Task SaveSettingsAsync(GoNoGoSettings s);
    string Compute(FlightPreparation fp, GoNoGoSettings s);
    string Compute(double? windKt, double? visKm, double? capeJkg, GoNoGoSettings s);
}
