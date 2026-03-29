using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;

namespace FlightPrep.Services;

public class PowerLineService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<PowerLineService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    // Serialize all Overpass requests: prevents burst 429s when multiple maps load simultaneously.
    // Combined with the in-cache re-check after acquiring, only one HTTP call is made per unique bbox.
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<string?> GetGeoJsonAsync(double south, double west, double north, double east)
    {
        // Round to 2 decimal places (~1 km grid) to maximise cache hits for similar viewports
        var s = Math.Round(south, 2);
        var w = Math.Round(west, 2);
        var n = Math.Round(north, 2);
        var e = Math.Round(east, 2);

        var cacheKey = FormattableString.Invariant($"powerlines:{s}:{w}:{n}:{e}");

        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        await _semaphore.WaitAsync();
        try
        {
            // Re-check after acquiring — another request may have fetched this bbox while we waited
            if (cache.TryGetValue(cacheKey, out cached))
                return cached;

            return await FetchWithRetryAsync(cacheKey, s, w, n, e);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string?> FetchWithRetryAsync(string cacheKey, double s, double w, double n, double e)
    {
        const string voltageFilter = "way[power=line][voltage~\"^([12][0-9]{5}|[3-9][0-9]{5})$\"];";
        var query = FormattableString.Invariant($"[out:json][timeout:25][bbox:{s},{w},{n},{e}];")
                    + voltageFilter + "out geom;";

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var client = httpClientFactory.CreateClient("overpass");
                using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
                using var response = await client.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt < 2)
                    {
                        logger.LogWarning("Overpass API 429 rate-limited, waiting 3 s before retry");
                        await Task.Delay(3000);
                        continue;
                    }
                    logger.LogWarning("Overpass API 429 after retry, skipping power-line fetch");
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var geoJson = ConvertToGeoJson(json);
                cache.Set(cacheKey, geoJson, CacheTtl);
                return geoJson;
            }
            catch (Exception ex) when (attempt < 2)
            {
                logger.LogWarning(ex, "Overpass API request failed (attempt {A}), retrying after 2 s", attempt);
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Overpass API request failed for bbox {S},{W},{N},{E}", s, w, n, e);
            }
        }
        return null;
    }

    private static string ConvertToGeoJson(string overpassJson)
    {
        using var doc = JsonDocument.Parse(overpassJson);
        var elements = doc.RootElement.GetProperty("elements");

        var features = new List<object>();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.GetProperty("type").GetString() != "way") continue;
            if (!element.TryGetProperty("geometry", out var geometry)) continue;

            var tags = element.TryGetProperty("tags", out var t)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(t.GetRawText()) ?? []
                : new Dictionary<string, string>();

            var coords = geometry.EnumerateArray()
                .Select(node => new[] { node.GetProperty("lon").GetDouble(), node.GetProperty("lat").GetDouble() })
                .ToArray();

            features.Add(new
            {
                type = "Feature",
                properties = tags,
                geometry = new { type = "LineString", coordinates = coords }
            });
        }

        return JsonSerializer.Serialize(new { type = "FeatureCollection", features });
    }
}
