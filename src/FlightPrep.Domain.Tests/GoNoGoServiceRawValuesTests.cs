using FlightPrep.Domain.Models;
using FlightPrep.Services;

namespace FlightPrep.Domain.Tests;

/// <summary>
///     Additional tests for the raw-values overload of
///     <see cref="GoNoGoService.Compute(double?, double?, double?, GoNoGoSettings)" />.
///     The existing <see cref="FlightPrep.Tests.GoNoGoServiceComputeTests" /> covers the
///     <c>Compute(FlightPreparation, GoNoGoSettings)</c> overload; this class focuses on
///     the new raw-values path used by the list page summary rows.
/// </summary>
public class GoNoGoServiceRawValuesTests
{
    private static readonly GoNoGoSettings DefaultSettings = new();

    // ── Red threshold ─────────────────────────────────────────────────────────

    [Fact]
    public void Compute_RawValues_WindAboveRedThreshold_ReturnsRed()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(20, null, null, DefaultSettings);

        // Assert
        Assert.Equal("red", result);
    }

    [Fact]
    public void Compute_RawValues_WindAtRedThreshold_ReturnsRed()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(15, null, null, DefaultSettings);

        // Assert — >= threshold triggers red
        Assert.Equal("red", result);
    }

    [Fact]
    public void Compute_RawValues_VisibilityBelowRedThreshold_ReturnsRed()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(null, 2.0, null, DefaultSettings);

        // Assert
        Assert.Equal("red", result);
    }

    [Fact]
    public void Compute_RawValues_CapeAboveRedThreshold_ReturnsRed()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(null, null, 600, DefaultSettings);

        // Assert
        Assert.Equal("red", result);
    }

    // ── Yellow threshold ──────────────────────────────────────────────────────

    [Fact]
    public void Compute_RawValues_WindAtYellowThreshold_ReturnsYellow()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(10, null, null, DefaultSettings);

        // Assert
        Assert.Equal("yellow", result);
    }

    [Fact]
    public void Compute_RawValues_VisibilityBelowYellowThreshold_ReturnsYellow()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(null, 4.0, null, DefaultSettings);

        // Assert
        Assert.Equal("yellow", result);
    }

    [Fact]
    public void Compute_RawValues_CapeAboveYellowThreshold_ReturnsYellow()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(null, null, 350, DefaultSettings);

        // Assert
        Assert.Equal("yellow", result);
    }

    // ── Green ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_RawValues_AllBelowThresholds_ReturnsGreen()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(5, 10, 100, DefaultSettings);

        // Assert
        Assert.Equal("green", result);
    }

    // ── Unknown ───────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_RawValues_NullValues_ReturnsUnknown()
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(null, null, null, DefaultSettings);

        // Assert
        Assert.Equal("unknown", result);
    }

    // ── Priority: red wins over yellow ────────────────────────────────────────

    [Theory]
    [InlineData(20.0, null, null)] // wind red
    [InlineData(null, 1.0, null)] // vis red
    [InlineData(null, null, 600.0)] // cape red
    public void Compute_RawValues_AnyRedCriterion_ReturnsRed(
        double? windKt, double? visKm, double? capeJkg)
    {
        // Arrange
        var sut = new GoNoGoService(null!);

        // Act
        var result = sut.Compute(windKt, visKm, capeJkg, DefaultSettings);

        // Assert
        Assert.Equal("red", result);
    }

    // ── Custom thresholds (regression for bug #23) ───────────────────────────

    [Fact]
    public void Compute_RawValues_CustomRedThreshold_WindBelowDefaultButAboveCustom_ReturnsRed()
    {
        // Arrange — custom red at 8 kt; default red at 15 kt
        var customSettings = new GoNoGoSettings { WindYellowKt = 5, WindRedKt = 8 };
        var sut = new GoNoGoService(null!);

        // Act — 10 kt is above custom red (8) but below default red (15)
        var result = sut.Compute(10, null, null, customSettings);

        // Assert
        Assert.Equal("red", result);
    }
}
