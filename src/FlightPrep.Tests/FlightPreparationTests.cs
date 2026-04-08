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

    // ── TotaalGewichtOFP ──────────────────────────────────────────────────────

    [Fact]
    public void TotaalGewichtOFP_AllWeightsProvided_ReturnsSumIncludingOffset()
    {
        // Arrange: envelope=250, basket=80, burner=20, cylinders=30, PIC=85
        //          one passenger 70 kg + equipment offset 7
        var fp = new FlightPreparation
        {
            OFPEnvelopeWeightKg = 250,
            OFPBasketWeightKg   = 80,
            OFPBurnerWeightKg   = 20,
            CylindersWeightKg   = 30,
            PicWeightKg         = 85
        };
        fp.Passengers.Add(new Passenger { WeightKg = 70 });

        // Act
        var result = fp.TotaalGewichtOFP(7);

        // Assert: 250 + 80 + 20 + 30 + 85 + (70 + 7) = 542
        Assert.Equal(542, result);
    }

    [Fact]
    public void TotaalGewichtOFP_NullOFPEquipmentFields_ContributeZero()
    {
        // Arrange: all OFP equipment fields are null, PIC = 80
        var fp = new FlightPreparation
        {
            OFPEnvelopeWeightKg = null,
            OFPBasketWeightKg   = null,
            OFPBurnerWeightKg   = null,
            CylindersWeightKg   = null,
            PicWeightKg         = 80
        };

        // Act
        var result = fp.TotaalGewichtOFP(5);

        // Assert: 0 + 0 + 0 + 0 + 80 + 0 (no passengers) = 80
        Assert.Equal(80, result);
    }

    [Fact]
    public void TotaalGewichtOFP_MultiplePassengers_OffsetAppliedToEach()
    {
        // Arrange: 3 passengers each 60 kg, equipment offset = 10
        var fp = new FlightPreparation
        {
            PicWeightKg = 0
        };
        fp.Passengers.Add(new Passenger { WeightKg = 60 });
        fp.Passengers.Add(new Passenger { WeightKg = 60 });
        fp.Passengers.Add(new Passenger { WeightKg = 60 });

        // Act
        var result = fp.TotaalGewichtOFP(10);

        // Assert: (60+10)*3 = 210
        Assert.Equal(210, result);
    }

    [Fact]
    public void TotaalGewichtOFP_PicWeightKgNull_FallsBackToPilotWeight()
    {
        // Arrange: PicWeightKg is null, but Pilot.WeightKg = 75
        var fp = new FlightPreparation
        {
            PicWeightKg = null,
            Pilot       = new Pilot { WeightKg = 75 }
        };

        // Act
        var result = fp.TotaalGewichtOFP(0);

        // Assert: pilot fallback = 75
        Assert.Equal(75, result);
    }

    [Fact]
    public void TotaalGewichtOFP_BothPicWeightAndPilotNull_ContributesZero()
    {
        // Arrange: both PicWeightKg and Pilot are null
        var fp = new FlightPreparation
        {
            PicWeightKg = null,
            Pilot       = null
        };

        // Act
        var result = fp.TotaalGewichtOFP(7);

        // Assert: PIC contribution = 0
        Assert.Equal(0, result);
    }

    [Fact]
    public void TotaalGewichtOFP_NoPassengers_ReturnsPicPlusEquipmentOnly()
    {
        // Arrange: equipment + PIC, no passengers
        var fp = new FlightPreparation
        {
            OFPEnvelopeWeightKg = 200,
            OFPBasketWeightKg   = 50,
            OFPBurnerWeightKg   = 15,
            CylindersWeightKg   = 25,
            PicWeightKg         = 90
        };
        // Passengers list intentionally empty

        // Act
        var result = fp.TotaalGewichtOFP(7);

        // Assert: 200 + 50 + 15 + 25 + 90 = 380, no passenger sum
        Assert.Equal(380, result);
    }
}
