using FlightPrep.Domain.Services;

namespace FlightPrep.Tests;

public class TrajectoryMathTests
{
    private const double R = 6_371_000;

    // ── Zero distance ─────────────────────────────────────────────────────────

    [Fact]
    public void HaversineDestination_ZeroDistance_ReturnsSamePoint()
    {
        var (lat, lon) = TrajectoryMath.HaversineDestination(51.0, 3.5, 90.0, 0.0);

        Assert.Equal(51.0, lat, 8);
        Assert.Equal(3.5, lon, 8);
    }

    // ── Cardinal directions ───────────────────────────────────────────────────

    [Fact]
    public void HaversineDestination_NorthBearing_IncreasesLatitude()
    {
        var (lat, lon) = TrajectoryMath.HaversineDestination(51.0, 3.5, 0.0, 10_000.0);

        Assert.True(lat > 51.0, $"North bearing should increase lat, got {lat:F6}");
        Assert.Equal(3.5, lon, 4);
    }

    [Fact]
    public void HaversineDestination_SouthBearing_DecreasesLatitude()
    {
        var (lat, _) = TrajectoryMath.HaversineDestination(51.0, 3.5, 180.0, 10_000.0);

        Assert.True(lat < 51.0, $"South bearing should decrease lat, got {lat:F6}");
    }

    [Fact]
    public void HaversineDestination_EastBearing_IncreasesLongitude()
    {
        var (_, lon) = TrajectoryMath.HaversineDestination(51.0, 3.5, 90.0, 10_000.0);

        Assert.True(lon > 3.5, $"East bearing should increase lon, got {lon:F6}");
    }

    [Fact]
    public void HaversineDestination_WestBearing_DecreasesLongitude()
    {
        var (_, lon) = TrajectoryMath.HaversineDestination(51.0, 3.5, 270.0, 10_000.0);

        Assert.True(lon < 3.5, $"West bearing should decrease lon, got {lon:F6}");
    }

    // ── Quantitative accuracy ─────────────────────────────────────────────────

    [Fact]
    public void HaversineDestination_1000mNorth_LatIncrementMatchesFormula()
    {
        // On a sphere of radius R, 1000 m along a meridian = 1000/(R * π/180) degrees
        const double distM = 1000.0;
        var expectedDeltaDeg = distM / (R * Math.PI / 180.0);

        var (lat, _) = TrajectoryMath.HaversineDestination(51.0, 3.5, 0.0, distM);

        Assert.Equal(expectedDeltaDeg, lat - 51.0, 6);
    }

    [Theory]
    [InlineData(1_000.0)]
    [InlineData(10_000.0)]
    [InlineData(100_000.0)]
    public void HaversineDestination_NorthBearing_DistanceIsCorrect(double distM)
    {
        var (lat2, _) = TrajectoryMath.HaversineDestination(51.0, 3.5, 0.0, distM);

        // Verify a round-trip: recompute distance from lat difference along meridian
        var actualDistM = (lat2 - 51.0) * Math.PI / 180.0 * R;
        Assert.Equal(distM, actualDistM, 0); // within 0.5 m
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void HaversineDestination_NorthThenSouth_ReturnsToOrigin()
    {
        var (lat2, lon2) = TrajectoryMath.HaversineDestination(51.0, 3.5, 0.0, 50_000.0);
        var (lat3, _) = TrajectoryMath.HaversineDestination(lat2, lon2, 180.0, 50_000.0);

        var errorM = Math.Abs(lat3 - 51.0) * (R * Math.PI / 180.0);
        Assert.True(errorM < 1.0, $"Round-trip error {errorM:F3} m exceeds 1 m tolerance");
    }

    [Fact]
    public void HaversineDestination_EastThenWest_ReturnsToOrigin()
    {
        // On a sphere, going east then west by the same great-circle distance
        // does NOT exactly return to the start because bearings shift as you move
        // (unlike north-south along a meridian). We verify only that both
        // longitude and latitude are close to the origin within ~600 m.
        var (lat2, lon2) = TrajectoryMath.HaversineDestination(0.0, 3.5, 90.0, 50_000.0);
        var (lat3, lon3) = TrajectoryMath.HaversineDestination(lat2, lon2, 270.0, 50_000.0);

        // At the equator (lat=0) east-west IS along a great circle parallel,
        // so the round-trip should be exact.
        var dLatM = Math.Abs(lat3 - 0.0) * (R * Math.PI / 180.0);
        var dLonM = Math.Abs(lon3 - 3.5) * (R * Math.PI / 180.0);
        Assert.True(dLatM + dLonM < 1.0,
            $"Equatorial round-trip error lat={dLatM:F3} m, lon={dLonM:F3} m (>1 m)");
    }
}
