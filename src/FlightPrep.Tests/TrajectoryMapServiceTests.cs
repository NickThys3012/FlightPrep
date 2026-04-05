using FlightPrep.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using System.Net;

namespace FlightPrep.Tests;

public class TrajectoryMapServiceTests
{
    // ── factory helpers ──────────────────────────────────────────────────────────

    private static TrajectoryMapService CreateSut(HttpMessageHandler handler)
        => new(new HttpClient(handler), NullLogger<TrajectoryMapService>.Instance);

    /// <summary>Generates a minimal valid 256×256 PNG tile as raw bytes.</summary>
    private static byte[] CreateTilePng()
    {
        using var surface = SKSurface.Create(new SKImageInfo(256, 256));
        surface.Canvas.Clear(new SKColor(189, 224, 255));
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // ── null / empty / invalid input ─────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_NullJson_ReturnsNull()
    {
        var sut = CreateSut(new FailingHandler());
        Assert.Null(await sut.RenderAsync(null));
    }

    [Fact]
    public async Task RenderAsync_WhitespaceJson_ReturnsNull()
    {
        var sut = CreateSut(new FailingHandler());
        Assert.Null(await sut.RenderAsync("   "));
    }

    [Fact]
    public async Task RenderAsync_EmptyArrayJson_ReturnsNull()
    {
        var sut = CreateSut(new FailingHandler());
        Assert.Null(await sut.RenderAsync("[]"));
    }

    [Fact]
    public async Task RenderAsync_MalformedJson_ReturnsNull()
    {
        // Exception caught internally → null, no crash
        var sut = CreateSut(new FailingHandler());
        Assert.Null(await sut.RenderAsync("{not valid json!"));
    }

    [Fact]
    public async Task RenderAsync_AllTrajectoriesFewerThan2Points_ReturnsNull()
    {
        // Single-point trajectories are filtered out; series is empty → null
        var json = """[{"AltitudeFt":500,"Color":"#FF0000","Points":[{"Lat":51.0,"Lon":4.0}]}]""";
        var sut = CreateSut(new FailingHandler());
        Assert.Null(await sut.RenderAsync(json));
    }

    // ── successful render paths ──────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_ValidTrajectory_TilesSucceed_ReturnsByteArray()
    {
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """
                   [{
                     "AltitudeFt": 1000,
                     "Color": "#FF6B6B",
                     "Points": [
                       {"Lat": 51.05, "Lon": 3.72},
                       {"Lat": 51.10, "Lon": 3.80},
                       {"Lat": 51.15, "Lon": 3.88},
                       {"Lat": 51.20, "Lon": 3.96}
                     ]
                   }]
                   """;

        var result = await sut.RenderAsync(json);

        Assert.NotNull(result);
        Assert.True(result.Length > 100, "Expected non-trivial JPEG output");
    }

    [Fact]
    public async Task RenderAsync_ValidTrajectory_TilesFail_StillReturnsByteArray()
    {
        // Tile HTTP calls fail → fallback ocean-blue canvas; trajectories still drawn
        var sut = CreateSut(new FailingHandler());
        var json = """
                   [{"AltitudeFt":500,"Color":"#4CAF50","Points":[
                     {"Lat":51.0,"Lon":4.0},
                     {"Lat":51.1,"Lon":4.1}
                   ]}]
                   """;

        var result = await sut.RenderAsync(json);

        Assert.NotNull(result);
        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task RenderAsync_MultipleTrajectories_ReturnsByteArray()
    {
        // Exercises legend rendering (multiple series) and DrawTrajectory looping
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """
                   [
                     {"AltitudeFt":  500, "Color": "#4CAF50", "Points": [{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.1},{"Lat":51.2,"Lon":4.2}]},
                     {"AltitudeFt": 1000, "Color": "#FF9800", "Points": [{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.2},{"Lat":51.2,"Lon":4.4}]},
                     {"AltitudeFt": 1500, "Color": "#F44336", "Points": [{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.3},{"Lat":51.2,"Lon":4.6}]}
                   ]
                   """;

        var result = await sut.RenderAsync(json);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task RenderAsync_ManyPoints_DrawsIntermediateDots()
    {
        // More than 6 points trigger the intermediate-dot drawing loop
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        // 12 points trigger the intermediate-dot drawing loop (every 3 points)
        var json = """
                   [{"AltitudeFt":1000,"Color":"#2196F3","Points":[
                     {"Lat":51.00,"Lon":4.00},{"Lat":51.02,"Lon":4.02},{"Lat":51.04,"Lon":4.04},
                     {"Lat":51.06,"Lon":4.06},{"Lat":51.08,"Lon":4.08},{"Lat":51.10,"Lon":4.10},
                     {"Lat":51.12,"Lon":4.12},{"Lat":51.14,"Lon":4.14},{"Lat":51.16,"Lon":4.16},
                     {"Lat":51.18,"Lon":4.18},{"Lat":51.20,"Lon":4.20},{"Lat":51.22,"Lon":4.22}
                   ]}]
                   """;

        var result = await sut.RenderAsync(json);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task RenderAsync_LargeExtent_TriggersResizeCodePath()
    {
        // A trajectory spanning ~20° longitude forces 8 tiles wide (8×256 = 2048 > 1200 MaxW),
        // exercising the SKBitmap resize branch.
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """
                   [{"AltitudeFt":1000,"Color":"#2196F3","Points":[
                     {"Lat":49.0,"Lon":-5.0},
                     {"Lat":51.0,"Lon":10.0},
                     {"Lat":52.0,"Lon":15.0}
                   ]}]
                   """;

        var result = await sut.RenderAsync(json);

        Assert.NotNull(result);
    }

    // ── ParseSkColor branches ────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_SixCharHexColor_ParsedCorrectly()
    {
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """[{"AltitudeFt":500,"Color":"#4CAF50","Points":[{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.1}]}]""";
        Assert.NotNull(await sut.RenderAsync(json));
    }

    [Fact]
    public async Task RenderAsync_EightCharHexColor_ParsedCorrectly()
    {
        // 8-char hex exercises the ARGB branch in ParseSkColor
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """[{"AltitudeFt":500,"Color":"FF4CAF50","Points":[{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.1}]}]""";
        Assert.NotNull(await sut.RenderAsync(json));
    }

    [Fact]
    public async Task RenderAsync_InvalidHexColor_FallsBackToBlueAndStillRenders()
    {
        // Invalid hex falls back to SKColors.Blue via the catch in ParseSkColor
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """[{"AltitudeFt":500,"Color":"notahex","Points":[{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.1}]}]""";
        Assert.NotNull(await sut.RenderAsync(json));
    }

    // ── mixed valid/invalid trajectories ────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_MixedValidAndSinglePointTrajectories_RendersValidOnes()
    {
        // One 1-point traj is filtered; one 2-point traj remains → renders successfully
        var sut = CreateSut(new SuccessHandler(CreateTilePng()));
        var json = """
                   [
                     {"AltitudeFt":500,  "Color":"#FF0000","Points":[{"Lat":51.0,"Lon":4.0}]},
                     {"AltitudeFt":1000, "Color":"#00FF00","Points":[{"Lat":51.0,"Lon":4.0},{"Lat":51.1,"Lon":4.1}]}
                   ]
                   """;
        Assert.NotNull(await sut.RenderAsync(json));
    }

    /// <summary>Handler that returns a 256×256 PNG for every tile request.</summary>
    private sealed class SuccessHandler(byte[] png) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) });
    }

    /// <summary>Handler that always returns 503 – simulates tile server outage.</summary>
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
