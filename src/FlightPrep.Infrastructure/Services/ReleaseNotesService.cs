using System.Text.Json;
using FlightPrep.Models.ReleaseNotes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace FlightPrep.Services;

public class ReleaseNotesService(
    IWebHostEnvironment env,
    ILogger<ReleaseNotesService> logger) : IReleaseNotesService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private ReleaseNotesDocument? _cache;
    private DateTime _cachedAt = DateTime.MinValue;

    public async Task<ReleaseNotesDocument> GetAsync()
    {
        if (_cache != null && (DateTime.UtcNow - _cachedAt).TotalMinutes < 5)
            return _cache;

        try
        {
            var path = Path.Combine(env.WebRootPath, "release-notes.json");
            var json = await File.ReadAllTextAsync(path);
            _cache = JsonSerializer.Deserialize<ReleaseNotesDocument>(json, JsonOpts) ?? new();
            _cachedAt = DateTime.UtcNow;
            logger.LogInformation("Release notes loaded: {Count} entries, v{Version}",
                _cache.Entries.Count, _cache.CurrentVersion);
            return _cache;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load release notes");
            return new();
        }
    }
}
