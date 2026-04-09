using FlightPrep.Domain.Models;

namespace FlightPrep.Tests;

/// <summary>
///     Unit tests for the computed <c>Balloon.EmptyWeightKg</c> property introduced in
///     feature/computed-empty-weight-and-pdf-fixes.
///
///     The property sums the four component weights (Envelope + Basket + Burner + Cylinders),
///     treating <c>null</c> as 0, and returns <c>null</c> when the total is zero.
/// </summary>
public class BalloonEmptyWeightTests
{
    // ── Returns null when all components are null ─────────────────────────────

    [Fact]
    public void EmptyWeightKg_AllComponentsNull_ReturnsNull()
    {
        // Arrange
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = null,
            BasketWeightKg       = null,
            BurnerWeightKg       = null,
            CylindersWeightKg    = null
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert
        Assert.Null(result);
    }

    // ── Returns null when all components are explicitly zero ──────────────────

    [Fact]
    public void EmptyWeightKg_AllComponentsZero_ReturnsNull()
    {
        // Arrange
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = 0,
            BasketWeightKg       = 0,
            BurnerWeightKg       = 0,
            CylindersWeightKg    = 0
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – sum is 0, so null must be returned
        Assert.Null(result);
    }

    // ── Returns correct sum when all four components are set ─────────────────

    [Fact]
    public void EmptyWeightKg_AllComponentsSet_ReturnsCorrectSum()
    {
        // Arrange
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = 250,
            BasketWeightKg       = 70,
            BurnerWeightKg       = 18,
            CylindersWeightKg    = 80
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – 250 + 70 + 18 + 80 = 418
        Assert.Equal(418, result);
    }

    // ── Returns correct sum when some components are null (treat as 0) ────────

    [Fact]
    public void EmptyWeightKg_EnvelopeNullOthersSet_TreatsNullAsZero()
    {
        // Arrange – only basket + burner + cylinders are set
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = null,
            BasketWeightKg       = 70,
            BurnerWeightKg       = 18,
            CylindersWeightKg    = 80
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – 0 + 70 + 18 + 80 = 168
        Assert.Equal(168, result);
    }

    [Fact]
    public void EmptyWeightKg_OnlyEnvelopeSet_ReturnsEnvelopeWeight()
    {
        // Arrange – only the envelope has a weight; others are null
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = 250,
            BasketWeightKg       = null,
            BurnerWeightKg       = null,
            CylindersWeightKg    = null
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – 250 + 0 + 0 + 0 = 250
        Assert.Equal(250, result);
    }

    [Fact]
    public void EmptyWeightKg_TwoComponentsNullTwoSet_ReturnsPartialSum()
    {
        // Arrange – burner and cylinders set; envelope and basket null
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = null,
            BasketWeightKg       = null,
            BurnerWeightKg       = 18,
            CylindersWeightKg    = 80
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – 0 + 0 + 18 + 80 = 98
        Assert.Equal(98, result);
    }

    // ── Returns null when the sum of non-null values is zero ─────────────────

    [Fact]
    public void EmptyWeightKg_MixOfNullAndZero_ReturnsNull()
    {
        // Arrange – one null, the rest explicitly zero → total is still 0
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = null,
            BasketWeightKg       = 0,
            BurnerWeightKg       = 0,
            CylindersWeightKg    = 0
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – total is 0, so null must be returned
        Assert.Null(result);
    }

    // ── Fractional weights are summed correctly ───────────────────────────────

    [Fact]
    public void EmptyWeightKg_FractionalComponents_ReturnsCorrectSum()
    {
        // Arrange – weights with decimal precision
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = 249.5,
            BasketWeightKg       = 70.2,
            BurnerWeightKg       = 17.8,
            CylindersWeightKg    = 80.5
        };

        // Act
        var result = balloon.EmptyWeightKg;

        // Assert – 249.5 + 70.2 + 17.8 + 80.5 = 418.0
        Assert.Equal(418.0, result);
    }
}
