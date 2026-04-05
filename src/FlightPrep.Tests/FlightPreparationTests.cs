using FlightPrep.Domain.Models;

namespace FlightPrep.Tests;

public class FlightPreparationTests
{
    private static FlightPreparation BuildFp(
        double? envelopeKg = null,
        double? pilotWeightKg = null,
        double? totalLiftKg = null) => new()
    {
        EnvelopeWeightKg = envelopeKg, Pilot = pilotWeightKg.HasValue ? new Pilot { WeightKg = pilotWeightKg.Value } : null, TotaalLiftKg = totalLiftKg
    };

    // ── TotaalGewicht ─────────────────────────────────────────────────────────

    [Fact]
    public void TotaalGewicht_NoData_ReturnsZero()
    {
        var fp = BuildFp();

        Assert.Equal(0, fp.TotaalGewicht);
    }

    [Fact]
    public void TotaalGewicht_EnvelopeOnly_ReturnsEnvelopeWeight()
    {
        var fp = BuildFp(200);

        Assert.Equal(200, fp.TotaalGewicht);
    }

    [Fact]
    public void TotaalGewicht_WithPilotAndPassengers_ReturnsSumOfAll()
    {
        var fp = BuildFp(200, 80);
        fp.Passengers.Add(new Passenger { WeightKg = 70 });
        fp.Passengers.Add(new Passenger { WeightKg = 65 });

        Assert.Equal(415, fp.TotaalGewicht);
    }

    [Fact]
    public void TotaalGewicht_PilotIsNull_DoesNotThrow()
    {
        var fp = BuildFp(100);

        Assert.Equal(100, fp.TotaalGewicht);
    }

    // ── LiftVoldoende ─────────────────────────────────────────────────────────

    [Fact]
    public void LiftVoldoende_LiftGreaterThanWeight_ReturnsTrue()
    {
        var fp = BuildFp(100, totalLiftKg: 500);

        Assert.True(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_LiftEqualToWeight_ReturnsFalse()
    {
        var fp = BuildFp(500, totalLiftKg: 500);

        Assert.False(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_LiftLessThanWeight_ReturnsFalse()
    {
        var fp = BuildFp(600, totalLiftKg: 500);

        Assert.False(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_LiftIsNull_ReturnsFalse()
    {
        var fp = BuildFp(100, totalLiftKg: null);

        Assert.False(fp.LiftVoldoende);
    }

    // ── GoNoGo ────────────────────────────────────────────────────────────────

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_NoMeteoData_ReturnsUnknown()
    {
        var fp = new FlightPreparation();

        Assert.Equal("unknown", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_WindAtRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 15 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_WindAboveRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 20 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_VisibilityBelowRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { ZichtbaarheidKm = 2.5 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_CapeAboveRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { CapeJkg = 600 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_WindAtYellowThreshold_ReturnsYellow()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 10 };

        Assert.Equal("yellow", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_VisibilityBelowYellowThreshold_ReturnsYellow()
    {
        var fp = new FlightPreparation { ZichtbaarheidKm = 4 };

        Assert.Equal("yellow", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_CapeAboveYellowThreshold_ReturnsYellow()
    {
        var fp = new FlightPreparation { CapeJkg = 400 };

        Assert.Equal("yellow", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_AllConditionsGood_ReturnsGreen()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 5, ZichtbaarheidKm = 10, CapeJkg = 100 };

        Assert.Equal("green", fp.GoNoGo);
    }

    [Fact]
    [Obsolete("Obsolete")]
    public void GoNoGo_OnlyWindDataPresent_UsesWindForDecision()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 8 };

        Assert.Equal("green", fp.GoNoGo);
    }

    // ── TotaalGewicht — additional edge cases ─────────────────────────────────

    [Fact]
    public void TotaalGewicht_WithPilotNoPassengers_ReturnsPilotPlusEnvelope()
    {
        // Arrange: envelope + pilot, zero passengers
        var fp = BuildFp(200, 80);
        // Passengers list is intentionally empty (default)

        // Act & Assert
        Assert.Equal(280, fp.TotaalGewicht);
    }

    [Theory]
    [InlineData(1, 70, 70)]
    [InlineData(2, 65, 130)]
    [InlineData(3, 80, 240)]
    public void TotaalGewicht_VaryingPassengerCount_SumsCorrectly(
        int count, double weightEach, double expectedPassengerTotal)
    {
        // Arrange
        var fp = BuildFp(0, 0);
        for (var i = 0; i < count; i++)
        {
            fp.Passengers.Add(new Passenger { WeightKg = weightEach });
        }

        // Act & Assert
        Assert.Equal(expectedPassengerTotal, fp.TotaalGewicht);
    }

    [Fact]
    public void TotaalGewicht_AllComponentsPresent_ReturnsCorrectSum()
    {
        // Arrange: envelope=250, pilot=85, pax=[70, 75]
        var fp = BuildFp(250, 85);
        fp.Passengers.Add(new Passenger { WeightKg = 70 });
        fp.Passengers.Add(new Passenger { WeightKg = 75 });

        // Act & Assert: 250 + 85 + 70 + 75 = 480
        Assert.Equal(480, fp.TotaalGewicht);
    }

    // ── LiftVoldoende — additional edge cases ─────────────────────────────────

    [Fact]
    public void LiftVoldoende_LiftJustAboveWeight_ReturnsTrue()
    {
        // Total weight = 300, lift = 300.01 → true
        var fp = BuildFp(300, totalLiftKg: 300.01);

        Assert.True(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_ZeroLift_ReturnsFalse()
    {
        var fp = BuildFp(100, totalLiftKg: 0);

        Assert.False(fp.LiftVoldoende);
    }
}
