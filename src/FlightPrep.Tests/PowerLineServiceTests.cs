using FlightPrep.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text.Json;

namespace FlightPrep.Tests;

public class PowerLineServiceTests
{
    private const string ValidOverpassJson = """
                                             {
                                                 "elements": [
                                                     {
                                                         "type": "way",
                                                         "id": 1,
                                                         "tags": { "power": "line", "voltage": "380000", "name": "Testlijn" },
                                                         "geometry": [
                                                             {"lat": 50.85, "lon": 4.35},
                                                             {"lat": 50.86, "lon": 4.36}
                                                         ]
                                                     }
                                                 ]
                                             }
                                             """;

    private static (PowerLineService sut, IMemoryCache cache, MockHttpMessageHandler handler) Build(
        string responseBody = ValidOverpassJson,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) });
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("overpass")).Returns(httpClient);
        var sut = new PowerLineService(factoryMock.Object, cache, NullLogger<PowerLineService>.Instance);
        return (sut, cache, handler);
    }

    [Fact]
    public async Task GetGeoJsonAsync_CacheHit_ReturnsWithoutHttpCall()
    {
        var (sut, _, handler) = Build();
        // Warm the cache with the first call
        var first = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);
        Assert.Equal(1, handler.CallCount);

        // The second call with an identical bbox must not hit the handler
        var second = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        Assert.Equal(1, handler.CallCount); // still 1 — no extra HTTP call
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetGeoJsonAsync_CacheMiss_StoresResultInCache()
    {
        var (sut, _, handler) = Build();

        await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);
        await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        // If a result was cached, the second call must not trigger a new HTTP request
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetGeoJsonAsync_SecondCallSameBbox_HitsCacheAndSkipsHttp()
    {
        var (sut, _, handler) = Build();

        await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);
        await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetGeoJsonAsync_CoordinatesRoundToSameBbox_HitsCache()
    {
        var (sut, _, handler) = Build();

        // Both round to s=50.85, w=4.35, n=50.86, e=4.36
        await sut.GetGeoJsonAsync(50.851, 4.351, 50.859, 4.359);
        await sut.GetGeoJsonAsync(50.853, 4.352, 50.856, 4.357);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetGeoJsonAsync_ValidResponse_ReturnsFeatureCollection()
    {
        var (sut, _, _) = Build();

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.Equal("FeatureCollection", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task GetGeoJsonAsync_ValidResponse_FeatureHasLineStringGeometry()
    {
        var (sut, _, _) = Build();

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        var feature = doc.RootElement.GetProperty("features")[0];
        Assert.Equal("Feature", feature.GetProperty("type").GetString());
        Assert.Equal("LineString", feature.GetProperty("geometry").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetGeoJsonAsync_ValidResponse_CoordinatesAreInLonLatOrder()
    {
        var (sut, _, _) = Build();

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        var coords = doc.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates");
        Assert.Equal(4.35, coords[0][0].GetDouble(), 5); // lon first
        Assert.Equal(50.85, coords[0][1].GetDouble(), 5); // lat second
    }

    [Fact]
    public async Task GetGeoJsonAsync_ValidResponse_TagsMappedToProperties()
    {
        var (sut, _, _) = Build();

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        var props = doc.RootElement.GetProperty("features")[0].GetProperty("properties");
        Assert.Equal("380000", props.GetProperty("voltage").GetString());
        Assert.Equal("Testlijn", props.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetGeoJsonAsync_HttpError_ReturnsNull()
    {
        var (sut, _, _) = Build("", HttpStatusCode.InternalServerError);

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetGeoJsonAsync_HttpError_DoesNotPopulateCache()
    {
        var (sut, cache, _) = Build("", HttpStatusCode.ServiceUnavailable);

        await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        Assert.False(cache.TryGetValue("powerlines:50.85:4.35:50.86:4.36", out _));
    }

    [Fact]
    public async Task GetGeoJsonAsync_NonWayElement_IsSkipped()
    {
        const string json = """
                            {
                                "elements": [
                                    { "type": "node", "id": 99, "tags": { "power": "tower" } }
                                ]
                            }
                            """;
        var (sut, _, _) = Build(json);

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        Assert.Equal(0, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task GetGeoJsonAsync_WayWithoutGeometry_IsSkipped()
    {
        const string json = """
                            {
                                "elements": [
                                    { "type": "way", "id": 1, "tags": { "power": "line", "voltage": "380000" } }
                                ]
                            }
                            """;
        var (sut, _, _) = Build(json);

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        Assert.Equal(0, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task GetGeoJsonAsync_WayWithoutTags_IncludedWithEmptyProperties()
    {
        const string json = """
                            {
                                "elements": [
                                    {
                                        "type": "way",
                                        "id": 1,
                                        "geometry": [
                                            {"lat": 50.85, "lon": 4.35},
                                            {"lat": 50.86, "lon": 4.36}
                                        ]
                                    }
                                ]
                            }
                            """;
        var (sut, _, _) = Build(json);

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.Equal(1, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task GetGeoJsonAsync_EmptyElements_ReturnsEmptyFeatureCollection()
    {
        const string json = """{"elements":[]}""";
        var (sut, _, _) = Build(json);

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        Assert.Equal("FeatureCollection", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task GetGeoJsonAsync_MixedElements_OnlyIncludesWaysWithGeometry()
    {
        const string json = """
                            {
                                "elements": [
                                    { "type": "node", "id": 1, "tags": {} },
                                    { "type": "way",  "id": 2, "tags": { "voltage": "220000" } },
                                    {
                                        "type": "way", "id": 3,
                                        "tags": { "voltage": "110000" },
                                        "geometry": [{"lat": 50.85, "lon": 4.35}, {"lat": 50.86, "lon": 4.36}]
                                    }
                                ]
                            }
                            """;
        var (sut, _, _) = Build(json);

        var result = await sut.GetGeoJsonAsync(50.85, 4.35, 50.86, 4.36);

        var doc = JsonDocument.Parse(result!);
        Assert.Equal(1, doc.RootElement.GetProperty("features").GetArrayLength());
    }
}

public class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(response);
    }
}
