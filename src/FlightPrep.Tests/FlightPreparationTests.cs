using FlightPrep.Models;

namespace FlightPrep.Tests;

public class FlightPreparationTests
{
    private static FlightPreparation BuildFp(
        double? envelopeKg = null,
        double? pilotWeightKg = null,
        double? totalLiftKg = null) => new()
    {
        EnvelopeWeightKg = envelopeKg,
        Pilot = pilotWeightKg.HasValue ? new Pilot { WeightKg = pilotWeightKg.Value } : null,
        TotaalLiftKg = totalLiftKg
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
        var fp = BuildFp(envelopeKg: 200);

        Assert.Equal(200, fp.TotaalGewicht);
    }

    [Fact]
    public void TotaalGewicht_WithPilotAndPassengers_ReturnsSumOfAll()
    {
        var fp = BuildFp(envelopeKg: 200, pilotWeightKg: 80);
        fp.Passengers.Add(new Passenger { WeightKg = 70 });
        fp.Passengers.Add(new Passenger { WeightKg = 65 });

        Assert.Equal(415, fp.TotaalGewicht);
    }

    [Fact]
    public void TotaalGewicht_PilotIsNull_DoesNotThrow()
    {
        var fp = BuildFp(envelopeKg: 100);

        Assert.Equal(100, fp.TotaalGewicht);
    }

    // ── LiftVoldoende ─────────────────────────────────────────────────────────

    [Fact]
    public void LiftVoldoende_LiftGreaterThanWeight_ReturnsTrue()
    {
        var fp = BuildFp(envelopeKg: 100, totalLiftKg: 500);

        Assert.True(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_LiftEqualToWeight_ReturnsFalse()
    {
        var fp = BuildFp(envelopeKg: 500, totalLiftKg: 500);

        Assert.False(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_LiftLessThanWeight_ReturnsFalse()
    {
        var fp = BuildFp(envelopeKg: 600, totalLiftKg: 500);

        Assert.False(fp.LiftVoldoende);
    }

    [Fact]
    public void LiftVoldoende_LiftIsNull_ReturnsFalse()
    {
        var fp = BuildFp(envelopeKg: 100, totalLiftKg: null);

        Assert.False(fp.LiftVoldoende);
    }

    // ── GoNoGo ────────────────────────────────────────────────────────────────

    [Fact]
    public void GoNoGo_NoMeteoData_ReturnsUnknown()
    {
        var fp = new FlightPreparation();

        Assert.Equal("unknown", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_WindAtRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 15 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_WindAboveRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 20 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_VisibilityBelowRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { ZichtbaarheidKm = 2.5 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_CapeAboveRedThreshold_ReturnsRed()
    {
        var fp = new FlightPreparation { CapeJkg = 600 };

        Assert.Equal("red", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_WindAtYellowThreshold_ReturnsYellow()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 10 };

        Assert.Equal("yellow", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_VisibilityBelowYellowThreshold_ReturnsYellow()
    {
        var fp = new FlightPreparation { ZichtbaarheidKm = 4 };

        Assert.Equal("yellow", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_CapeAboveYellowThreshold_ReturnsYellow()
    {
        var fp = new FlightPreparation { CapeJkg = 400 };

        Assert.Equal("yellow", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_AllConditionsGood_ReturnsGreen()
    {
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 5,
            ZichtbaarheidKm = 10,
            CapeJkg = 100
        };

        Assert.Equal("green", fp.GoNoGo);
    }

    [Fact]
    public void GoNoGo_OnlyWindDataPresent_UsesWindForDecision()
    {
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 8 };

        Assert.Equal("green", fp.GoNoGo);
    }
}
