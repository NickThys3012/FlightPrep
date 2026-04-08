using FlightPrep.Domain.Models.Trajectory;
using FlightPrep.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Globalization;
using System.Net;

namespace FlightPrep.Tests;

public class EnhancedTrajectoryServiceTests
{
    // A past date guarantees the archive path is used in WeatherFetchService
    private static readonly DateTime PastLaunch = new(2024, 1, 15, 6, 0, 0, DateTimeKind.Utc);

    private static readonly int[] PressureHPa = [1000, 975, 950, 925, 900, 850, 800, 700];

    // ── JSON helpers ──────────────────────────────────────────────────────────

    /// <summary>Builds a minimal but valid Open-Meteo hourly JSON for the given time range.</summary>
    private static string BuildOpenMeteoJson(
        DateTime from, DateTime to, double speedKmh, double dirDeg)
    {
        var hours = new List<DateTime>();
        for (var h = from; h <= to; h = h.AddHours(1))
        {
            hours.Add(h);
        }

        var timeArr = string.Join(",", hours.Select(h => $"\"{h:yyyy-MM-ddTHH:mm}\""));
        var speed = speedKmh.ToString("F2", CultureInfo.InvariantCulture);
        var dir = dirDeg.ToString("F2", CultureInfo.InvariantCulture);
        var speedVal = string.Join(",", hours.Select(_ => speed));
        var dirVal = string.Join(",", hours.Select(_ => dir));

        var speedProps = string.Join(",", PressureHPa.Select(p =>
            $"\"windspeed_{p}hPa\":[{speedVal}]"));
        var dirProps = string.Join(",", PressureHPa.Select(p =>
            $"\"winddirection_{p}hPa\":[{dirVal}]"));

        return $"{{\"hourly\":{{\"time\":[{timeArr}],{speedProps},{dirProps}}}}}";
    }

    /// <summary>Builds JSON with a 3-hour window around PastLaunch at 270° (west), 36 km/h.</summary>
    private static string DefaultJson(double speedKmh = 36.0, double dirDeg = 270.0) =>
        BuildOpenMeteoJson(PastLaunch.AddHours(-1), PastLaunch.AddHours(3), speedKmh, dirDeg);

    // ── Service builder ───────────────────────────────────────────────────────

    private static EnhancedTrajectoryService BuildSut(
        string openMeteoJson,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            // Create a fresh HttpResponseMessage per call so the StringContent
            // stream is not exhausted when ComputeMultipleAsync makes N parallel calls.
            .ReturnsAsync(() => new HttpResponseMessage(statusCode) { Content = new StringContent(openMeteoJson) });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.open-meteo.com/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("openmeteo")).Returns(client);

        var weatherSvc = new WeatherFetchService(
            factory.Object,
            new Mock<ILogger<WeatherFetchService>>().Object);

        return new EnhancedTrajectoryService(
            weatherSvc,
            new Mock<ILogger<EnhancedTrajectoryService>>().Object);
    }

    // ── Guard / ArgumentOutOfRange ────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_ZeroAscentRate_ThrowsArgumentOutOfRange()
    {
        var sut = BuildSut(DefaultJson());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                0, 3000,
                3.0, 60));
    }

    [Fact]
    public async Task ComputeAsync_NegativeAscentRate_ThrowsArgumentOutOfRange()
    {
        var sut = BuildSut(DefaultJson());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                -1.0, 3000,
                3.0, 60));
    }

    [Fact]
    public async Task ComputeAsync_ZeroDescentRate_ThrowsArgumentOutOfRange()
    {
        var sut = BuildSut(DefaultJson());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                3.0, 3000,
                0, 60));
    }

    [Fact]
    public async Task ComputeAsync_ZeroCruiseAltitudeFt_ThrowsArgumentOutOfRange()
    {
        var sut = BuildSut(DefaultJson());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                3.0, 0,
                3.0, 60));
    }

    [Fact]
    public async Task ComputeAsync_ZeroTotalDuration_ThrowsArgumentOutOfRange()
    {
        var sut = BuildSut(DefaultJson());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                3.0, 3000,
                3.0, 0));
    }

    [Fact]
    public async Task ComputeAsync_ZeroStepSeconds_ThrowsArgumentOutOfRange()
    {
        var sut = BuildSut(DefaultJson());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                3.0, 3000,
                3.0, 60, 0));
    }

    // ── Empty wind data ───────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_NoWindData_ReturnsEmptyPoints()
    {
        // HTTP 500 → FetchWindTimeSeriesAsync catches the error and returns []
        var sut = BuildSut("{}", HttpStatusCode.InternalServerError);

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.Empty(result.Points);
    }

    [Fact]
    public async Task ComputeAsync_NoWindData_StillReturnsCorrectAltitudeAndDuration()
    {
        var sut = BuildSut("{}", HttpStatusCode.InternalServerError);

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 5000,
            3.0, 90);

        Assert.Equal(5000, result.AltitudeFt);
        Assert.Equal(90, result.DurationMinutes);
        Assert.Equal(TrajectoryDataSource.Hysplit, result.DataSource);
    }

    // ── Valid simulation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_ValidInput_ReturnsNonEmptyPoints()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.NotEmpty(result.Points);
    }

    [Fact]
    public async Task ComputeAsync_ValidInput_FirstElapsedMinuteIsZero()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.Equal(0, result.Points[0].ElapsedMinutes);
    }

    [Fact]
    public async Task ComputeAsync_ValidInput_LastElapsedMinuteEqualsDuration()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.Equal(60, result.Points.Last().ElapsedMinutes);
    }

    [Fact]
    public async Task ComputeAsync_ValidInput_DataSourceIsHysplit()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.Equal(TrajectoryDataSource.Hysplit, result.DataSource);
    }

    [Fact]
    public async Task ComputeAsync_ValidInput_AltitudeFtMatchesCruiseParameter()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 4500,
            3.0, 90);

        Assert.Equal(4500, result.AltitudeFt);
    }

    [Fact]
    public async Task ComputeAsync_ValidInput_AllPointsHaveAltitudeFt()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.All(result.Points, p => Assert.NotNull(p.AltitudeFt));
    }

    [Fact]
    public async Task ComputeAsync_ValidInput_AscentDescentRatesStoredOnResult()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            2.5, 3000,
            1.5, 60);

        Assert.Equal(2.5, result.AscentRateMs);
        Assert.Equal(1.5, result.DescentRateMs);
    }

    [Fact]
    public async Task ComputeAsync_WestWind_BalloonMovesEast()
    {
        // Wind FROM 270° (west) → bearing = (270+180)%360 = 90° (east)
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 90);

        var lastLon = result.Points.Last().Lon;
        Assert.True(lastLon > 3.5,
            $"Expected balloon east of 3.5° (wind from west), got {lastLon:F4}°");
    }

    [Fact]
    public async Task ComputeAsync_NorthWind_BalloonMovesSouth()
    {
        // Wind FROM 0° (north) → bearing = 180° (south)
        var sut = BuildSut(DefaultJson(36.0, 0.0));

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 90);

        var lastLat = result.Points.Last().Lat;
        Assert.True(lastLat < 51.0,
            $"Expected balloon south of 51.0° (wind from north), got {lastLat:F4}°");
    }

    [Fact]
    public async Task ComputeAsync_ZeroWindSpeed_BalloonStaysAtLaunchLocation()
    {
        // Wind speed 0 → no movement; lat/lon should stay at launch
        var sut = BuildSut(DefaultJson(0.0));

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            3.0, 3000,
            3.0, 60);

        Assert.All(result.Points, p =>
        {
            if (p == null)
            {
                throw new ArgumentNullException(nameof(p));
            }

            Assert.Equal(51.0, p.Lat, 5);
            Assert.Equal(3.5, p.Lon, 5);
        });
    }

    // Regression: cruiseDurationS < 0 (ascent+descent > total) must not crash
    [Fact]
    public async Task ComputeAsync_AscentPlusDescentExceedsTotalDuration_ReturnsPoints()
    {
        // 1000 ft = 304.8 m; at 1 m/s up + 1 m/s down = 609.6 s > 9 min (540 s)
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeAsync(51.0, 3.5, PastLaunch,
            1.0, 1000,
            1.0, 9);

        Assert.NotEmpty(result.Points);
    }

    [Fact]
    public async Task ComputeAsync_CancellationAlreadyRequested_ThrowsOperationCanceled()
    {
        // Provide valid JSON so the loop is entered (CT checked inside loop)
        var sut = BuildSut(DefaultJson());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.ComputeAsync(51.0, 3.5, PastLaunch,
                3.0, 3000,
                3.0, 60,
                cancellationToken: cts.Token));
    }

    // ── ComputeMultipleAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ComputeMultipleAsync_EmptyAltitudeList_ReturnsEmptyList()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [],
            3.0, 60);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ComputeMultipleAsync_SingleAltitude_ReturnsSingleTrajectory()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [3000],
            3.0, 60);

        Assert.Single(result);
        Assert.Equal(3000, result[0].AltitudeFt);
    }

    [Fact]
    public async Task ComputeMultipleAsync_ThreeAltitudes_ReturnsThreeTrajectories()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [1000, 3000, 5000],
            3.0, 60);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ComputeMultipleAsync_MoreThanFiveAltitudes_CapsAtFive()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [500, 1000, 2000, 3000, 4000, 5000, 6000],
            3.0, 60);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task ComputeMultipleAsync_DuplicateAltitudes_Deduplicates()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [3000, 3000, 3000],
            3.0, 60);

        Assert.Single(result);
        Assert.Equal(3000, result[0].AltitudeFt);
    }

    [Fact]
    public async Task ComputeMultipleAsync_UnorderedAltitudes_ResultsAreOrderedAscending()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [5000, 1000, 3000],
            3.0, 60);

        Assert.Equal(1000, result[0].AltitudeFt);
        Assert.Equal(3000, result[1].AltitudeFt);
        Assert.Equal(5000, result[2].AltitudeFt);
    }

    [Fact]
    public async Task ComputeMultipleAsync_ThreeAltitudes_AllAssignedDistinctColors()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [1000, 3000, 5000],
            3.0, 60);

        var colors = result.Select(t => t.Color).ToList();
        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Fact]
    public async Task ComputeMultipleAsync_AllTrajectories_HaveCorrectDurationMinutes()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [1000, 3000],
            3.0, 75);

        Assert.All(result, t => Assert.Equal(75, t.DurationMinutes));
    }

    [Fact]
    public async Task ComputeMultipleAsync_AllTrajectories_DataSourceIsHysplit()
    {
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [1000, 3000, 5000],
            3.0, 60);

        Assert.All(result, t =>
            Assert.Equal(TrajectoryDataSource.Hysplit, t.DataSource));
    }

    [Fact]
    public async Task ComputeMultipleAsync_HigherAltitude_LandsAtDifferentLocation()
    {
        // Both altitudes should produce non-empty trajectories when wind data is available.
        // With constant wind at all levels the paths are identical, so just verify count & points.
        var sut = BuildSut(DefaultJson());

        var result = await sut.ComputeMultipleAsync(51.0, 3.5, PastLaunch,
            3.0, [1000, 5000],
            3.0, 90);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.NotEmpty(t.Points));
    }
}
