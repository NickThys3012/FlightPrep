using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

public interface IOFPSettingsService
{
    Task<OFPSettings> GetSettingsAsync(string? userId);
    Task SaveSettingsAsync(OFPSettings s, string? userId);
}
