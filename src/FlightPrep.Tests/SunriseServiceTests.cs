using FlightPrep.Services;

namespace FlightPrep.Tests;

public class SunriseServiceTests
{
    private readonly SunriseService _sut = new();

    [Fact]
    public void Calculate_Brussels_20260323_MatchesExpectedTimes()
    {
        var (sunrise, sunset) = _sut.Calculate(new DateOnly(2026, 3, 23), 50.85, 4.35);

        // NOAA algorithm produces sunrise ~05:39 UTC, sunset ~17:59 UTC for this date/location (±5 min)
        int srMin = sunrise.Hour * 60 + sunrise.Minute;
        int ssMin = sunset.Hour  * 60 + sunset.Minute;

        Assert.True(Math.Abs(srMin - (5 * 60 + 39)) <= 5,
            $"Sunrise {sunrise} is not within 5 min of expected 05:39 UTC");
        Assert.True(Math.Abs(ssMin - (17 * 60 + 59)) <= 5,
            $"Sunset {sunset} is not within 5 min of expected 17:59 UTC");
    }

    [Fact]
    public void Calculate_ArcticSummer_ProducesNearMidnightTime()
    {
        // lat=70°N, lon=25°E on summer solstice → polar day; cosHA clamped to -1
        // The clamped calculation yields a near-midnight result (hour < 4 or hour ≥ 22)
        var (sunrise, _) = _sut.Calculate(new DateOnly(2026, 6, 21), 70.0, 25.0);

        Assert.True(sunrise.Hour < 4 || sunrise.Hour >= 22,
            $"Expected near-midnight Arctic sunrise (hour < 4 or ≥ 22) for polar summer, got {sunrise}");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(51.5, -0.12)]
    [InlineData(-33.8, 151.2)]
    [InlineData(64.1, -21.9)]
    [InlineData(-70.0, 0.0)]
    public void Calculate_DoesNotThrow_ForAnyValidInput(double lat, double lon)
    {
        var exception = Record.Exception(() =>
            _sut.Calculate(new DateOnly(2026, 6, 15), lat, lon));

        Assert.Null(exception);
    }
}
