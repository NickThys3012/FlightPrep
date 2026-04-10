using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FlightPrep.Tests;

/// <summary>
///     Tests for <see cref="FlightAssessmentService" /> — both the synchronous
///     <c>Compute(fp, settings)</c> overload and the async <c>ComputeAsync(fp)</c>
///     overload that loads settings from the database.
/// </summary>
public class FlightAssessmentServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Builds a <see cref="FlightAssessmentService" /> backed by a mock
    ///     <see cref="IGoNoGoService" /> whose <c>Compute</c> overload returns
    ///     the supplied <paramref name="goNoGoResult" />.
    /// </summary>
    private static FlightAssessmentService BuildSut(string goNoGoResult = "green")
    {
        var mock = new Mock<IGoNoGoService>();
        mock.Setup(s => s.Compute(
                It.IsAny<double?>(),
                It.IsAny<double?>(),
                It.IsAny<double?>(),
                It.IsAny<GoNoGoSettings>()))
            .Returns(goNoGoResult);
        return new FlightAssessmentService(mock.Object);
    }

    /// <summary>
    ///     Builds a <see cref="FlightAssessmentService" /> backed by the real
    ///     <see cref="GoNoGoService" /> with a null db factory (safe because only
    ///     the synchronous <c>Compute</c> path is exercised).
    /// </summary>
    private static FlightAssessmentService BuildSutWithRealGoNoGo()
        => new(new GoNoGoService(null!));

    private static GoNoGoSettings DefaultSettings() => new();

    // ── EF in-memory factory for async tests ──────────────────────────────────

    private static IDbContextFactory<AppDbContext> CreateDbFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    // ── TotaalGewicht: sync Compute ───────────────────────────────────────────

    [Fact]
    public void Compute_TypicalFlight_ReturnsTotaalGewichtSum()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation { EnvelopeWeightKg = 200, Pilot = new Pilot { WeightKg = 80 }, TotaalLiftKg = 1000 };
        fp.Passengers.Add(new Passenger { WeightKg = 70 });
        fp.Passengers.Add(new Passenger { WeightKg = 65 });

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert — 200 + 80 + 70 + 65 = 415
        Assert.Equal(415, result.TotaalGewicht);
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
    public void Compute_PilotWithNullWeight_TreatsAsPilotWeightZero()
    {
        // Arrange — pilot exists, but WeightKg is null
        var sut = BuildSut();
        var fp = new FlightPreparation { EnvelopeWeightKg = 100, Pilot = new Pilot { WeightKg = null }, TotaalLiftKg = 500 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert — only envelope weight counts
        Assert.Equal(100, result.TotaalGewicht);
    }

    // ── LiftVoldoende: sync Compute ───────────────────────────────────────────

    [Fact]
    public void Compute_LiftAboveWeight_ReturnsLiftVoldoendeTrue()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation { EnvelopeWeightKg = 100, TotaalLiftKg = 500 };

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
        var fp = new FlightPreparation { EnvelopeWeightKg = 600, TotaalLiftKg = 400 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.False(result.LiftVoldoende);
    }

    [Fact]
    public void Compute_LiftEqualToWeight_ReturnsLiftVoldoendeFalse()
    {
        // Arrange — strict greater-than: equal is NOT enough
        var sut = BuildSut();
        var fp = new FlightPreparation { EnvelopeWeightKg = 300, TotaalLiftKg = 300 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.False(result.LiftVoldoende);
    }

    [Fact]
    public void Compute_NullLift_ReturnsLiftVoldoendeFalse()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation { EnvelopeWeightKg = 100, TotaalLiftKg = null };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.False(result.LiftVoldoende);
    }

    // ── GoNoGo delegation: sync Compute ──────────────────────────────────────

    [Fact]
    public void Compute_WindAboveRedThreshold_ReturnsGoNoGoRed()
    {
        // Arrange — real GoNoGoService, wind 20 > default red (15 kt)
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 20, TotaalLiftKg = 1000 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("red", result.GoNoGo);
    }

    [Fact]
    public void Compute_WindBetweenYellowAndRed_ReturnsGoNoGoYellow()
    {
        // Arrange — wind 12 is between yellow (10) and red (15)
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 12, TotaalLiftKg = 1000 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("yellow", result.GoNoGo);
    }

    [Fact]
    public void Compute_AllConditionsGood_ReturnsGoNoGoGreen()
    {
        // Arrange
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 5, VisibilityKm = 15, CapeJkg = 50, TotaalLiftKg = 1000 };

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
        var fp = new FlightPreparation { TotaalLiftKg = 1000 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("unknown", result.GoNoGo);
    }

    [Fact]
    public void Compute_MockedGoNoGoReturnsRed_AssessmentReflectsRed()
    {
        // Arrange — mock ensures GoNoGo == "red" regardless of inputs
        var sut = BuildSut("red");
        var fp = new FlightPreparation { TotaalLiftKg = 500, EnvelopeWeightKg = 100 };

        // Act
        var result = sut.Compute(fp, DefaultSettings());

        // Assert
        Assert.Equal("red", result.GoNoGo);
    }

    // ── Null-argument guards: sync Compute ────────────────────────────────────

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

    // ── Custom settings: sync Compute ─────────────────────────────────────────

    /// <summary>
    ///     Regression for bug #23 — GoNoGo was ignoring pilot-configured
    ///     GoNoGoSettings and using hardcoded thresholds.
    ///     Wind = 10 kt must be "red" with a custom red threshold of 8 kt.
    /// </summary>
    [Fact]
    public void Compute_CustomSettings_RespectsCustomThresholds()
    {
        // Arrange — custom red at 8 kt (stricter than default 15 kt)
        var customSettings = new GoNoGoSettings
        {
            WindYellowKt = 5,
            WindRedKt = 8,
            VisYellowKm = 10,
            VisRedKm = 5,
            CapeYellowJkg = 200,
            CapeRedJkg = 400
        };
        var sut = BuildSutWithRealGoNoGo();
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 10, TotaalLiftKg = 1000 };

        // Act
        var result = sut.Compute(fp, customSettings);

        // Assert — 10 kt > custom red (8) → must be "red"
        Assert.Equal("red", result.GoNoGo);
    }

    // ── Async ComputeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_NullFlightPreparation_Throws()
    {
        // Arrange — use a real GoNoGoService backed by an in-memory DB
        var factory = CreateDbFactory(nameof(ComputeAsync_NullFlightPreparation_Throws));
        var sut = new FlightAssessmentService(new GoNoGoService(factory));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.ComputeAsync(null!));
    }

    [Fact]
    public async Task ComputeAsync_NoSettingsInDb_UsesDefaultSettings()
    {
        // Arrange — empty DB; GoNoGoService.GetSettingsAsync returns new GoNoGoSettings()
        var factory = CreateDbFactory(nameof(ComputeAsync_NoSettingsInDb_UsesDefaultSettings));
        var sut = new FlightAssessmentService(new GoNoGoService(factory));
        var fp = new FlightPreparation
        {
            EnvelopeWeightKg = 100,
            Pilot = new Pilot { WeightKg = 80 },
            SurfaceWindSpeedKt = 5,
            VisibilityKm = 20,
            CapeJkg = 50,
            TotaalLiftKg = 1000
        };
        fp.Passengers.Add(new Passenger { WeightKg = 60 });

        // Act
        var result = await sut.ComputeAsync(fp);

        // Assert — 100 + 80 + 60 = 240; lift (1000) > weight → LiftVoldoende = true
        Assert.Equal(240, result.TotaalGewicht);
        Assert.True(result.LiftVoldoende);
        Assert.Equal("green", result.GoNoGo);
    }

    [Fact]
    public async Task ComputeAsync_WithSettingsInDb_UsesPersistedSettings()
    {
        // Arrange — save custom thresholds so a red threshold is 8 kt
        var factory = CreateDbFactory(nameof(ComputeAsync_WithSettingsInDb_UsesPersistedSettings));
        var goNoGoSvc = new GoNoGoService(factory);
        var sut = new FlightAssessmentService(goNoGoSvc);

        await goNoGoSvc.SaveSettingsAsync(new GoNoGoSettings
        {
            WindYellowKt = 5,
            WindRedKt = 8,
            VisYellowKm = 10,
            VisRedKm = 5,
            CapeYellowJkg = 200,
            CapeRedJkg    = 400
        }, null);

        // Wind = 10 kt exceeds custom red (8) → should be "red"
        var fp = new FlightPreparation { SurfaceWindSpeedKt = 10, TotaalLiftKg = 500, EnvelopeWeightKg = 100 };

        // Act
        var result = await sut.ComputeAsync(fp);

        // Assert
        Assert.Equal("red", result.GoNoGo);
    }

    [Fact]
    public async Task ComputeAsync_NullLift_ReturnsLiftVoldoendeFalse()
    {
        // Arrange
        var factory = CreateDbFactory(nameof(ComputeAsync_NullLift_ReturnsLiftVoldoendeFalse));
        var sut = new FlightAssessmentService(new GoNoGoService(factory));
        var fp = new FlightPreparation { EnvelopeWeightKg = 100, TotaalLiftKg = null };

        // Act
        var result = await sut.ComputeAsync(fp);

        // Assert
        Assert.False(result.LiftVoldoende);
    }

    [Fact]
    public async Task ComputeAsync_TypicalFlight_TotaalGewichtMatchesSyncResult()
    {
        // Arrange — async and sync overloads must produce the same TotaalGewicht
        var factory = CreateDbFactory(nameof(ComputeAsync_TypicalFlight_TotaalGewichtMatchesSyncResult));
        var goNoGoSvc = new GoNoGoService(factory);
        var sut = new FlightAssessmentService(goNoGoSvc);

        var fp = new FlightPreparation { EnvelopeWeightKg = 180, Pilot = new Pilot { WeightKg = 75 }, TotaalLiftKg = 800 };
        fp.Passengers.Add(new Passenger { WeightKg = 70 });
        fp.Passengers.Add(new Passenger { WeightKg = 55 });

        // Act
        var asyncResult = await sut.ComputeAsync(fp);
        var syncResult = sut.Compute(fp, new GoNoGoSettings());

        // Assert — same weight regardless of overload
        Assert.Equal(syncResult.TotaalGewicht, asyncResult.TotaalGewicht);
    }

    [Fact]
    public async Task ComputeAsync_NoMeteoData_ReturnsGoNoGoUnknown()
    {
        // Arrange — no wind/vis/cape → "unknown"
        var factory = CreateDbFactory(nameof(ComputeAsync_NoMeteoData_ReturnsGoNoGoUnknown));
        var sut = new FlightAssessmentService(new GoNoGoService(factory));
        var fp = new FlightPreparation { TotaalLiftKg = 500, EnvelopeWeightKg = 100 };

        // Act
        var result = await sut.ComputeAsync(fp);

        // Assert
        Assert.Equal("unknown", result.GoNoGo);
    }

    // ── Per-user thresholds: async ComputeAsync ───────────────────────────────

    [Fact]
    public async Task ComputeAsync_WithPerUserSettings_UsesUserThresholds()
    {
        // Arrange — save custom thresholds for a specific user (red at 5 kt, much stricter than default 15 kt)
        var factory   = CreateDbFactory(nameof(ComputeAsync_WithPerUserSettings_UsesUserThresholds));
        var goNoGoSvc = new GoNoGoService(factory);
        var sut       = new FlightAssessmentService(goNoGoSvc);
        const string userId = "pilot-user-1";

        await goNoGoSvc.SaveSettingsAsync(new GoNoGoSettings
        {
            WindRedKt    = 5,
            WindYellowKt = 3,
            VisRedKm     = 3,
            VisYellowKm  = 5,
            CapeRedJkg   = 500,
            CapeYellowJkg = 300,
            UserId       = userId
        }, userId);

        // Wind = 8 kt is above the custom red threshold (5), but below the default red (15)
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 8,
            TotaalLiftKg       = 500,
            EnvelopeWeightKg   = 100
        };

        // Act
        var result = await sut.ComputeAsync(fp, userId);

        // Assert — must be "red" because 8 kt > custom threshold 5 kt
        Assert.Equal("red", result.GoNoGo);
    }

    [Fact]
    public async Task ComputeAsync_DifferentUsersGetDifferentThresholds()
    {
        // Arrange — user1 has strict thresholds (red at 5 kt), user2 has lenient (red at 20 kt)
        var factory   = CreateDbFactory(nameof(ComputeAsync_DifferentUsersGetDifferentThresholds));
        var goNoGoSvc = new GoNoGoService(factory);
        var sut       = new FlightAssessmentService(goNoGoSvc);

        await goNoGoSvc.SaveSettingsAsync(new GoNoGoSettings
        {
            WindRedKt    = 5,
            WindYellowKt = 3,
            VisRedKm     = 3,
            VisYellowKm  = 5,
            CapeRedJkg   = 500,
            CapeYellowJkg = 300,
            UserId       = "user1"
        }, "user1");

        await goNoGoSvc.SaveSettingsAsync(new GoNoGoSettings
        {
            WindRedKt    = 20,
            WindYellowKt = 15,
            VisRedKm     = 3,
            VisYellowKm  = 5,
            CapeRedJkg   = 500,
            CapeYellowJkg = 300,
            UserId       = "user2"
        }, "user2");

        // Wind = 10 kt is between the two thresholds
        var fp = new FlightPreparation
        {
            SurfaceWindSpeedKt = 10,
            TotaalLiftKg       = 500,
            EnvelopeWeightKg   = 100
        };

        // Act
        var result1 = await sut.ComputeAsync(fp, "user1");
        var result2 = await sut.ComputeAsync(fp, "user2");

        // Assert — user1: 10 > 5 → red; user2: 10 < 20 → not red
        Assert.Equal("red", result1.GoNoGo);
        Assert.NotEqual("red", result2.GoNoGo);
    }
}
