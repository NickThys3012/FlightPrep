using System.Text.Json;
using FlightPrep.Models.ReleaseNotes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace FlightPrep.Services;

public class ReleaseNotesService(
    IHttpClientFactory httpFactory,
    IWebHostEnvironment env,
    ILogger<ReleaseNotesService> logger)
{
    private static readonly string RawUrl =
        "https://raw.githubusercontent.com/NickThys3012/FlightPrep/main/src/FlightPrep/wwwroot/release-notes.json";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private ReleaseNotesDocument? _cache;
    private DateTime _cachedAt = DateTime.MinValue;

    public async Task<ReleaseNotesDocument> GetAsync()
    {
        if (_cache != null && (DateTime.UtcNow - _cachedAt).TotalMinutes < 5)
            return _cache;

        // Try GitHub raw first — always reflects latest commit, no deploy needed
        try
        {
            var client = httpFactory.CreateClient("githubraw");
            var json = await client.GetStringAsync(RawUrl);
            _cache = JsonSerializer.Deserialize<ReleaseNotesDocument>(json, JsonOpts) ?? new();
            _cachedAt = DateTime.UtcNow;
            logger.LogInformation("Release notes loaded from GitHub ({Count} entries, v{Version})",
                _cache.Entries.Count, _cache.CurrentVersion);
            return _cache;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch release notes from GitHub, falling back to local file");
        }

        // Fallback: local wwwroot file (baked into image at deploy time)
        try
        {
            var path = Path.Combine(env.WebRootPath, "release-notes.json");
            var json = await File.ReadAllTextAsync(path);
            _cache = JsonSerializer.Deserialize<ReleaseNotesDocument>(json, JsonOpts) ?? new();
            _cachedAt = DateTime.UtcNow;
            logger.LogInformation("Release notes loaded from local file ({Count} entries)", _cache.Entries.Count);
            return _cache;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load release notes from local file");
            return new();
        }
    }
}
