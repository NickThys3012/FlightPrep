using FlightPrep.Services;

namespace FlightPrep.Domain.Tests;

/// <summary>
/// Unit tests for <see cref="LiftCalculator.Calculate"/>.
/// Covers the ISA-based total lift formula from the Belgian Hot Air Balloon
/// Flight Manual, Amendment 18, Appendix 2, Page A2-1.
/// All expected values are pre-computed from the published formula.
/// </summary>
public class LiftCalculatorTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Standard baseline parameters used across multiple tests.</summary>
    private static LiftResult Baseline(double A = 1000, double Eg = 20,
                                       double Tg = 15,  double Ti = 80,
                                       double V  = 2200)
        => LiftCalculator.Calculate(A, Eg, Tg, Ti, V);

    // ── 1. Typical flight — all three output values are verified ─────────────

    [Fact]
    public void Calculate_TypicalFlight_ReturnsExpectedAmbientTemp()
    {
        // Arrange: A=1000, Eg=20 → ta_at = 15 - (0.0065 × 980) = 8.63 °C
        // Act
        var result = Baseline();

        // Assert
        Assert.Equal(8.63, result.AmbientTempAtAltC);
    }

    [Fact]
    public void Calculate_TypicalFlight_ReturnsExpectedPressure()
    {
        // Arrange: ISA pressure at 1000 m AMSL ≈ 898.74 hPa
        // Act
        var result = Baseline();

        // Assert — within 0.1 hPa of analytical result
        Assert.InRange(result.PressureHpa, 898.6, 898.9);
    }

    [Fact]
    public void Calculate_TypicalFlight_ReturnsPositiveLift()
    {
        // Arrange: Ti(80°C) > ta_at(8.63°C) → hot air is lighter → positive lift
        // Act
        var result = Baseline();

        // Assert — expected 494.1 kg for these inputs
        Assert.Equal(494.1, result.TotalLiftKg);
        Assert.True(result.TotalLiftKg > 0, "lift must be positive when envelope is hotter than ambient");
    }

    // ── 2. Ti capping at 100 °C ───────────────────────────────────────────────

    [Fact]
    public void Calculate_TiExceeds100_IsCappedAt100()
    {
        // Arrange
        var withTi120 = LiftCalculator.Calculate(1000, 20, 15, 120, 2200);
        var withTi100 = LiftCalculator.Calculate(1000, 20, 15, 100, 2200);

        // Assert — results must be identical because 120 is capped to 100
        Assert.Equal(withTi100.AmbientTempAtAltC, withTi120.AmbientTempAtAltC);
        Assert.Equal(withTi100.PressureHpa,        withTi120.PressureHpa);
        Assert.Equal(withTi100.TotalLiftKg,        withTi120.TotalLiftKg);
    }

    [Fact]
    public void Calculate_TiAt100_NotCapped_UsedAsIs()
    {
        // Arrange: Ti exactly at the cap — must not be reduced
        var result = LiftCalculator.Calculate(1000, 20, 15, 100, 2200);

        // Assert: expected value pre-computed (Ti=100 → L≈598.6 kg)
        Assert.Equal(598.6, result.TotalLiftKg);
    }

    // ── 3. A == Eg → ambient temperature at altitude equals ground temperature ─

    [Fact]
    public void Calculate_AEqualsEg_AmbientTempEqualsGroundTemp()
    {
        // Arrange: altitude gain = 0 → no lapse rate applied
        const double tg = 15.0;
        var result = LiftCalculator.Calculate(A: 50, Eg: 50, Tg: tg, Ti: 80, V: 2200);

        // Assert — ta_at = Tg - 0.0065 × 0 = Tg
        Assert.Equal(tg, result.AmbientTempAtAltC);
    }

    // ── 4. Zero volume → zero lift ────────────────────────────────────────────

    [Fact]
    public void Calculate_ZeroVolume_ReturnsZeroLift()
    {
        // Arrange
        var result = LiftCalculator.Calculate(1000, 20, 15, 80, V: 0);

        // Assert
        Assert.Equal(0.0, result.TotalLiftKg);
    }

    // ── 5. Higher altitude → lower pressure → lower lift ─────────────────────

    [Fact]
    public void Calculate_HigherAltitude_LowerLift()
    {
        // Arrange: everything identical except altitude
        var low  = LiftCalculator.Calculate(A: 500,  Eg: 20, Tg: 15, Ti: 80, V: 2200);
        var high = LiftCalculator.Calculate(A: 2000, Eg: 20, Tg: 15, Ti: 80, V: 2200);

        // Assert: lower air density at 2000 m means less lift
        Assert.True(high.TotalLiftKg < low.TotalLiftKg,
            $"lift at 2000 m ({high.TotalLiftKg}) must be less than at 500 m ({low.TotalLiftKg})");
    }

    // ── 6. Higher Ti → larger temperature differential → higher lift ──────────

    [Fact]
    public void Calculate_HigherTi_HigherLift()
    {
        // Arrange
        var cool = LiftCalculator.Calculate(1000, 20, 15, Ti: 60, V: 2200);
        var hot  = LiftCalculator.Calculate(1000, 20, 15, Ti: 90, V: 2200);

        // Assert
        Assert.True(hot.TotalLiftKg > cool.TotalLiftKg,
            $"Ti=90 ({hot.TotalLiftKg}) must produce more lift than Ti=60 ({cool.TotalLiftKg})");
    }

    // ── 7. Intermediate values cross-check ────────────────────────────────────

    [Fact]
    public void Calculate_IntermediateValues_AreCorrect()
    {
        // Arrange: A=1000, Eg=20, Tg=15, Ti=80, V=2200
        var result = Baseline();

        // Assert AmbientTempAtAltC ≈ 8.63 (within 0.01)
        Assert.InRange(result.AmbientTempAtAltC, 8.62, 8.64);

        // Assert PressureHpa ≈ 898.74 (within 0.1)
        Assert.InRange(result.PressureHpa, 898.6, 898.9);
    }

    // ── 8. Negative lift when Ti < ambient ────────────────────────────────────

    [Fact]
    public void Calculate_NegativeLift_WhenTiLessThanAmbient_DoesNotThrow()
    {
        // Arrange: Ti=5°C < ta_at(≈8.63°C) → envelope colder than ambient → negative lift
        // The formula must NOT throw; it just returns a negative number
        var result = LiftCalculator.Calculate(1000, 20, 15, Ti: 5, V: 2200);

        // Assert: formula completes and returns negative lift
        Assert.True(result.TotalLiftKg < 0,
            "lift must be negative when envelope temperature is below ambient temperature");
    }

    // ── 9. Volume scales lift linearly ────────────────────────────────────────

    [Fact]
    public void Calculate_LargeVolume_ScalesLinearly()
    {
        // Arrange: double the volume
        var half   = LiftCalculator.Calculate(1000, 20, 15, 80, V: 1100);
        var single = LiftCalculator.Calculate(1000, 20, 15, 80, V: 2200);
        var dbl    = LiftCalculator.Calculate(1000, 20, 15, 80, V: 4400);

        // Assert: lift ∝ V — within rounding tolerance of 0.2 kg
        Assert.InRange(single.TotalLiftKg, half.TotalLiftKg * 2 - 0.2, half.TotalLiftKg * 2 + 0.2);
        Assert.InRange(dbl.TotalLiftKg,    single.TotalLiftKg * 2 - 0.2, single.TotalLiftKg * 2 + 0.2);
    }

    // ── 10. Ti = 0 does not throw ─────────────────────────────────────────────

    [Fact]
    public void Calculate_TiAt0_DoesNotThrow()
    {
        // Arrange: Ti=0 → 1/(0+273.15) is perfectly valid
        var exception = Record.Exception(() => LiftCalculator.Calculate(1000, 20, 15, Ti: 0, V: 2200));

        // Assert
        Assert.Null(exception);
    }

    // ── 11. LiftResult record equality ───────────────────────────────────────

    [Fact]
    public void LiftResult_SameValues_AreEqual()
    {
        // Arrange
        var a = new LiftResult(8.63, 898.74, 494.1);
        var b = new LiftResult(8.63, 898.74, 494.1);

        // Assert: record value equality
        Assert.Equal(a, b);
    }
}
