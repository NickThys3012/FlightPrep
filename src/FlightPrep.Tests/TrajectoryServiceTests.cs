using FlightPrep.Models;
using FlightPrep.Models.Trajectory;
using FlightPrep.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FlightPrep.Tests;

public class TrajectoryServiceTests
{
    private static readonly TrajectoryService Sut = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WindLevel Wind(int altFt, int dirDeg, int speedKt) => new()
    {
        AltitudeFt   = altFt,
        DirectionDeg = dirDeg,
        SpeedKt      = speedKt,
        Order        = 1,
    };

    private static WindLevel WindNullSpeed(int altFt, int dirDeg) => new()
    {
        AltitudeFt   = altFt,
        DirectionDeg = dirDeg,
        SpeedKt      = null,
        Order        = 1,
    };

    private static WindLevel WindNullDir(int altFt, int speedKt) => new()
    {
        AltitudeFt   = altFt,
        DirectionDeg = null,
        SpeedKt      = speedKt,
        Order        = 1,
    };

    /// <summary>
    /// Haversine great-circle distance in metres between two lat/lon points.
    /// </summary>
    private static double HaversineDistanceM(
        double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Point count
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_WithSingleWindLevel_ReturnsCorrectPointCount()
    {
        // Arrange
        var levels = new[] { Wind(1000, 270, 10) };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, durationMinutes: 60, stepMinutes: 5);

        // Assert – 60/5 = 12 steps + 1 origin = 13 points
        Assert.Single(result);
        Assert.Equal(13, result[0].Points.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2–5. Directional movement
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_WindFromWest270_BalloonMovesEast()
    {
        // Arrange – wind FROM west (270°) → bearing 90° → lon increases
        var levels = new[] { Wind(1000, 270, 10) };

        // Act
        var points = Sut.Compute(50.85, 4.35, levels, 60, 5)[0].Points;

        // Assert – every successive point has a greater longitude
        for (int i = 1; i < points.Count; i++)
            Assert.True(points[i].Lon > points[i - 1].Lon,
                $"Expected lon[{i}] > lon[{i-1}], got {points[i].Lon} vs {points[i-1].Lon}");
    }

    [Fact]
    public void Compute_WindFromNorth360_BalloonMovesSouth()
    {
        // Arrange – wind FROM north (360°) → bearing 180° → lat decreases
        var levels = new[] { Wind(1000, 360, 10) };

        // Act
        var points = Sut.Compute(50.85, 4.35, levels, 60, 5)[0].Points;

        // Assert
        for (int i = 1; i < points.Count; i++)
            Assert.True(points[i].Lat < points[i - 1].Lat,
                $"Expected lat[{i}] < lat[{i-1}], got {points[i].Lat} vs {points[i-1].Lat}");
    }

    [Fact]
    public void Compute_WindFromEast90_BalloonMovesWest()
    {
        // Arrange – wind FROM east (90°) → bearing 270° → lon decreases
        var levels = new[] { Wind(1000, 90, 10) };

        // Act
        var points = Sut.Compute(50.85, 4.35, levels, 60, 5)[0].Points;

        // Assert
        for (int i = 1; i < points.Count; i++)
            Assert.True(points[i].Lon < points[i - 1].Lon,
                $"Expected lon[{i}] < lon[{i-1}], got {points[i].Lon} vs {points[i-1].Lon}");
    }

    [Fact]
    public void Compute_WindFromSouth180_BalloonMovesNorth()
    {
        // Arrange – wind FROM south (180°) → bearing 0° → lat increases
        var levels = new[] { Wind(1000, 180, 10) };

        // Act
        var points = Sut.Compute(50.85, 4.35, levels, 60, 5)[0].Points;

        // Assert
        for (int i = 1; i < points.Count; i++)
            Assert.True(points[i].Lat > points[i - 1].Lat,
                $"Expected lat[{i}] > lat[{i-1}], got {points[i].Lat} vs {points[i-1].Lat}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6–8. Invalid wind level filtering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_ZeroSpeedWindLevel_IsExcluded()
    {
        // Arrange
        var levels = new[] { new WindLevel { AltitudeFt = 1000, DirectionDeg = 270, SpeedKt = 0 } };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Compute_NullSpeedWindLevel_IsExcluded()
    {
        // Arrange
        var levels = new[] { WindNullSpeed(1000, 270) };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Compute_NullDirectionWindLevel_IsExcluded()
    {
        // Arrange
        var levels = new[] { WindNullDir(1000, 10) };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert
        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9–10. Mixed valid / invalid
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_TwoValidOneMixedInvalidWindLevels_ReturnsOnlyValid()
    {
        // Arrange – level 2 has null SpeedKt (invalid)
        var levels = new[]
        {
            Wind(1000, 270, 10),
            WindNullSpeed(2000, 180),
            Wind(3000, 90, 15),
        };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Compute_ThreeValidWindLevels_ReturnsThreeTrajectories()
    {
        // Arrange
        var levels = new[]
        {
            Wind(1000, 270, 10),
            Wind(2000, 180, 15),
            Wind(3000,  90, 20),
        };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert
        Assert.Equal(3, result.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Ordering by altitude
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_ResultOrderedByAltitudeAscending()
    {
        // Arrange – supply in descending order
        var levels = new[]
        {
            Wind(9840, 270, 20),
            Wind(4920,  90, 15),
            Wind( 360, 180, 10),
        };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert
        Assert.Equal(360,  result[0].AltitudeFt);
        Assert.Equal(4920, result[1].AltitudeFt);
        Assert.Equal(9840, result[2].AltitudeFt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12–13. Color palette
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_AssignsDistinctColorsToSevenLevels()
    {
        // Arrange – exactly 7 levels (palette size)
        var levels = Enumerable.Range(0, 7)
            .Select(i => Wind(1000 + i * 500, 270, 10 + i))
            .ToArray();

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert – all 7 colors are distinct
        Assert.Equal(7, result.Select(t => t.Color).Distinct().Count());
    }

    [Fact]
    public void Compute_EightLevels_CyclesColorPalette()
    {
        // Arrange – 8 levels forces wrap-around
        var levels = Enumerable.Range(0, 8)
            .Select(i => Wind(1000 + i * 500, 270, 10 + i))
            .ToArray();

        // Act
        // Result is ordered by altitude, so result[0] == lowest alt, result[7] == highest.
        // Colors are assigned in order of AltitudeFt (sorted ascending by the service),
        // so colorIndex 0 → result[0], colorIndex 7 → result[7].
        // Palette has 7 entries; index 7 % 7 == 0 → same as index 0.
        var result = Sut.Compute(50.85, 4.35, levels, 60);

        // Assert – 8th trajectory (index 7) gets same color as 1st (index 0)
        Assert.Equal(result[0].Color, result[7].Color);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14–15. Metadata propagation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_DataSourcePropagatedToAllTrajectories()
    {
        // Arrange
        var levels = new[] { Wind(1000, 270, 10), Wind(2000, 180, 15) };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, 60,
            dataSource: TrajectoryDataSource.OpenMeteo);

        // Assert
        Assert.All(result, t => Assert.Equal(TrajectoryDataSource.OpenMeteo, t.DataSource));
    }

    [Fact]
    public void Compute_DurationMinutesPropagatedToAllTrajectories()
    {
        // Arrange
        var levels = new[] { Wind(1000, 270, 10), Wind(2000, 180, 15) };

        // Act
        var result = Sut.Compute(50.85, 4.35, levels, durationMinutes: 45);

        // Assert
        Assert.All(result, t => Assert.Equal(45, t.DurationMinutes));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 16. Empty input
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_EmptyWindLevels_ReturnsEmptyList()
    {
        // Act
        var result = Sut.Compute(50.85, 4.35, Array.Empty<WindLevel>(), 60);

        // Assert
        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 17. Haversine accuracy
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_HaversineAccuracy_10ktWindFor60Min_ExpectedDistance()
    {
        // Arrange – 10 kt wind due north, 60-min flight, 5-min steps
        // Expected total distance = 10 kt × 1852 m/kt = 18 520 m
        var levels = new[] { Wind(1000, 180, 10) }; // FROM south → bearing 0° (north)
        const double expectedM = 10 * 1852.0;        // 18 520 m

        // Act
        var points = Sut.Compute(50.0, 4.35, levels, 60, 5)[0].Points;
        var first  = points.First();
        var last   = points.Last();
        double actualM = HaversineDistanceM(first.Lat, first.Lon, last.Lat, last.Lon);

        // Assert – within 100 m tolerance (floating-point accumulation over 12 steps)
        Assert.InRange(actualM, expectedM - 100, expectedM + 100);
    }
}
