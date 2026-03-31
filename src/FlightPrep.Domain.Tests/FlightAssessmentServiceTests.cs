using FlightPrep.Models;
using FlightPrep.Services;
using Moq;

namespace FlightPrep.Domain.Tests;

/// <summary>
/// Pure unit tests for <see cref="FlightAssessmentService.Compute(FlightPreparation, GoNoGoSettings)"/>.
/// No database, no HTTP — the sync overload is fully deterministic.
/// </summary>
public class FlightAssessmentServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FlightAssessmentService BuildSut(string goNoGoResult = "green")
    {
        var mockGoNoGo = new Mock<IGoNoGoService>();
        mockGoNoGo
            .Setup(s => s.Compute(
                It.IsAny<double?>(),
                It.IsAny<double?>(),
                It.IsAny<double?>(),
                It.IsAny<GoNoGoSettings>()))
            .Returns(goNoGoResult);
        return new FlightAssessmentService(mockGoNoGo.Object);
    }

    private static FlightAssessmentService BuildSutWithRealGoNoGo()
    {
        // Use the real GoNoGoService (null db factory is fine — only Compute() is called)
        var realGoNoGo = new GoNoGoService(null!);
        return new FlightAssessmentService(realGoNoGo);
    }

    private static GoNoGoSettings DefaultSettings() => new();

    // ── TotaalGewicht ─────────────────────────────────────────────────────────

    [Fact]
    public void Compute_TypicalFlight_ReturnsTotaalGewichtSum()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            EnvelopeWeightKg = 200,
            Pilot = new Pilot { WeightKg = 80 },
            TotaalLiftKg = 1000
        };
        fp.Passengers.Add(new Passenger { WeightKg = 70 });
        fp.Passengers.Add(new Passenger { WeightKg = 65 });

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert — 200 + 80 + 70 + 65 = 415
        Assert.Equal(415, result.TotaalGewicht);
    }

    [Fact]
    public void Compute_NoEnvelopeOrPilot_TotaalGewichtIsPassengerWeightOnly()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation { TotaalLiftKg = 500 };
        fp.Passengers.Add(new Passenger { WeightKg = 90 });

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal(90, result.TotaalGewicht);
    }

    [Fact]
    public void Compute_NullEnvelopeAndNullPilot_TotaalGewichtIsZero()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation { TotaalLiftKg = 500 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal(0, result.TotaalGewicht);
    }

    // ── LiftVoldoende ─────────────────────────────────────────────────────────

    [Fact]
    public void Compute_LiftAboveWeight_ReturnsLiftVoldoendeTrue()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            EnvelopeWeightKg = 100,
            TotaalLiftKg = 500      // lift >> weight
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.True(result.LiftVoldoende);
    }

    [Fact]
    public void Compute_LiftBelowWeight_ReturnsLiftVoldoendeFalse()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            EnvelopeWeightKg = 600,
            TotaalLiftKg = 400      // lift < weight
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.False(result.LiftVoldoende);
    }

    [Fact]
    public void Compute_LiftEqualToWeight_ReturnsLiftVoldoendeFalse()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            EnvelopeWeightKg = 300,
            TotaalLiftKg = 300
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert — strict greater-than: equal is NOT sufficient
        Assert.False(result.LiftVoldoende);
    }

    [Fact]
    public void Compute_NullLift_ReturnsLiftVoldoendeFalse()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            EnvelopeWeightKg = 100,
            TotaalLiftKg = null
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.False(result.LiftVoldoende);
    }

    // ── GoNoGo delegation ─────────────────────────────────────────────────────

    [Fact]
    public void Compute_WindAboveRedThreshold_ReturnsGoNoGoRed()
    {
        // Arrange — use the real GoNoGoService so we test the full chain
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 20,   // above default red (15 kt)
            TotaalLiftKg = 1000
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("red", result.GoNoGo);
    }

    [Fact]
    public void Compute_WindBetweenYellowAndRed_ReturnsGoNoGoYellow()
    {
        // Arrange
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 12,   // between yellow (10) and red (15)
            TotaalLiftKg = 1000
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("yellow", result.GoNoGo);
    }

    [Fact]
    public void Compute_WindBelowYellowThreshold_ReturnsGoNoGoGreen()
    {
        // Arrange
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 5,    // below yellow (10 kt)
            TotaalLiftKg = 1000
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("green", result.GoNoGo);
    }

    [Fact]
    public void Compute_NullWeatherData_ReturnsGoNoGoUnknown()
    {
        // Arrange
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = null,
            ZichtbaarheidKm = null,
            CapeJkg = null,
            TotaalLiftKg = 1000
        };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("unknown", result.GoNoGo);
    }

    /// <summary>
    /// Regression for bug #23 — GoNoGo was ignoring pilot-configured GoNoGoSettings
    /// and always using hardcoded thresholds baked into the entity property.
    /// With custom thresholds (red at 8 kt), wind=10 must return "red".
    /// </summary>
    [Fact]
    public void Compute_CustomSettings_RespectsCustomThresholds()
    {
        // Arrange — custom settings: red at 8 kt (stricter than default 15 kt)
        var customSettings = new GoNoGoSettings
        {
            WindYellowKt  = 5,
            WindRedKt     = 8,
            VisYellowKm   = 10,
            VisRedKm      = 5,
            CapeYellowJkg = 200,
            CapeRedJkg    = 400
        };
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 10,   // above custom red (8), but below default red (15)
            TotaalLiftKg = 1000
        };

        // Act
        var result = sut.Compute(fp, customSettings);

        // Assert — must be "red" using custom thresholds, not "yellow" using defaults
        Assert.Equal("red", result.GoNoGo);
    }

    // ── Null-argument guards ──────────────────────────────────────────────────

    [Fact]
    public void Compute_NullFlightPreparation_Throws()
    {
        // Arrange
        var sut = BuildSut();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            sut.Compute(null!, DefaultSettings()));
    }

    [Fact]
    public void Compute_NullSettings_Throws()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            sut.Compute(fp, null!));
    }
}
