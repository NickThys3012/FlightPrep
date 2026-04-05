using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Tests;

/// <summary>
///     Tests for <see cref="FlightPreparationSummary" /> — verifies the record maps
///     its constructor parameters correctly to named properties.
/// </summary>
public class FlightPreparationSummaryTests
{
    [Fact]
    public void Constructor_AllFieldsProvided_MapsToCorrectProperties()
    {
        // Arrange
        var datum = new DateOnly(2024, 6, 15);
        var tijdstip = new TimeOnly(6, 30);

        // Act
        var summary = new FlightPreparationSummary(
            42,
            datum,
            tijdstip,
            true,
            "OO-BUT",
            "Jan Peeters",
            "Leuven",
            8.5,
            10,
            250,
            null);

        // Assert
        Assert.Equal(42, summary.Id);
        Assert.Equal(datum, summary.Datum);
        Assert.Equal(tijdstip, summary.Tijdstip);
        Assert.True(summary.IsFlown);
        Assert.Equal("OO-BUT", summary.BalloonRegistration);
        Assert.Equal("Jan Peeters", summary.PilotName);
        Assert.Equal("Leuven", summary.LocationName);
        Assert.Equal(8.5, summary.SurfaceWindSpeedKt);
        Assert.Equal(10, summary.ZichtbaarheidKm);
        Assert.Equal(250, summary.CapeJkg);
    }

    [Fact]
    public void Constructor_NullableFieldsNull_MapsNullsCorrectly()
    {
        // Arrange & Act
        var summary = new FlightPreparationSummary(
            1,
            DateOnly.MinValue,
            TimeOnly.MinValue,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        // Assert
        Assert.Null(summary.BalloonRegistration);
        Assert.Null(summary.PilotName);
        Assert.Null(summary.LocationName);
        Assert.Null(summary.SurfaceWindSpeedKt);
        Assert.Null(summary.ZichtbaarheidKm);
        Assert.Null(summary.CapeJkg);
        Assert.False(summary.IsFlown);
    }

    [Fact]
    public void Record_SameValues_AreEqual()
    {
        // Arrange
        var a = new FlightPreparationSummary(1, DateOnly.MinValue, TimeOnly.MinValue, false, null, null, null, null, null, null, null);
        var b = new FlightPreparationSummary(1, DateOnly.MinValue, TimeOnly.MinValue, false, null, null, null, null, null, null, null);

        // Assert — records with identical values must be equal
        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_DifferentId_AreNotEqual()
    {
        // Arrange
        var a = new FlightPreparationSummary(1, DateOnly.MinValue, TimeOnly.MinValue, false, null, null, null, null, null, null, null);
        var b = new FlightPreparationSummary(2, DateOnly.MinValue, TimeOnly.MinValue, false, null, null, null, null, null, null, null);

        // Assert
        Assert.NotEqual(a, b);
    }
}
