using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FlightPrep.Tests;

/// <summary>
///     Additional GoNoGo coverage for issues #38:
///     custom threshold overrides, yellow/red ordering, persistence round-trip,
///     and consistency between the two Compute overloads.
/// </summary>
public class GoNoGoServiceIssueCoverageTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static IDbContextFactory<AppDbContext> CreateFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    private static FlightPreparation FpWith(
        double? wind = null, double? vis = null, double? cape = null) =>
        new() { SurfaceWindSpeedKt = wind, ZichtbaarheidKm = vis, CapeJkg = cape };

    // ── Custom threshold: wind red ────────────────────────────────────────────

    /// <summary>
    ///     A pilot raises the wind-red threshold to 25 kt.
    ///     Wind at 20 kt would be red under the 15 kt default but must NOT be red
    ///     under the pilot's custom 25 kt threshold.
    /// </summary>
    [Fact]
    public void GoNoGoService_Compute_CustomWindRedThreshold_OverridesDefault()
    {
        // Arrange
        var sut = new GoNoGoService(null!);
        var custom = new GoNoGoSettings { WindYellowKt = 18, WindRedKt = 25 };
        var fp = FpWith(wind: 20); // 20 ≥ default red (15) but < custom red (25)

        // Act
        var result = sut.Compute(fp, custom);

        // Assert — must NOT be red; custom threshold (25 kt) governs
        Assert.NotEqual("red", result);
        // 20 ≥ custom yellow (18) → yellow
        Assert.Equal("yellow", result);
    }

    // ── Custom threshold: visibility red ─────────────────────────────────────

    /// <summary>
    ///     A pilot lowers the vis-red threshold to 1 km.
    ///     Visibility at 2 km is below the default red (3 km) but above the custom red (1 km)
    ///     so the result must not be red.
    /// </summary>
    [Fact]
    public void GoNoGoService_Compute_CustomVisRedThreshold_OverridesDefault()
    {
        // Arrange
        var sut = new GoNoGoService(null!);
        // Relax: red only when vis < 1 km (vs. default 3 km)
        var custom = new GoNoGoSettings { VisYellowKm = 2, VisRedKm = 1 };
        var fp = FpWith(vis: 2.5); // 2.5 < default red (3) but > custom red (1)

        // Act
        var result = sut.Compute(fp, custom);

        // Assert — must NOT be red under the relaxed threshold
        Assert.NotEqual("red", result);
        // 2.5 < VisYellowKm (2) is false for 2.5 → not yellow from vis alone → green
        Assert.Equal("green", result);
    }

    // ── Custom threshold: CAPE yellow ─────────────────────────────────────────

    /// <summary>
    ///     A pilot raises the CAPE yellow threshold to 500 J/kg.
    ///     CAPE at 400 J/kg is above the default yellow (300) but below the custom yellow (500)
    ///     so the result must be green, not yellow.
    /// </summary>
    [Fact]
    public void GoNoGoService_Compute_CustomCapeYellowThreshold_OverridesDefault()
    {
        // Arrange
        var sut = new GoNoGoService(null!);
        var custom = new GoNoGoSettings { CapeYellowJkg = 500, CapeRedJkg = 800 };
        var fp = FpWith(cape: 400); // 400 ≥ default yellow (300) but < custom yellow (500)

        // Act
        var result = sut.Compute(fp, custom);

        // Assert — must be green; the custom yellow threshold is 500, not 300
        Assert.Equal("green", result);
    }

    // ── Yellow < Red ordering ─────────────────────────────────────────────────

    /// <summary>
    ///     When both yellow and red custom thresholds are provided, a value in the
    ///     yellow zone (≥ yellow, &lt; red) must return "yellow", not "red".
    ///     This verifies the threshold ordering contract: yellow threshold &lt; red threshold.
    /// </summary>
    [Fact]
    public void GoNoGoService_Compute_YellowMustBeLessThanRed_WhenBothProvided()
    {
        // Arrange
        var sut = new GoNoGoService(null!);
        var custom = new GoNoGoSettings { WindYellowKt = 12, WindRedKt = 20 };
        var fp = FpWith(wind: 15); // 15 ≥ yellow (12), but 15 < red (20)

        // Act
        var result = sut.Compute(fp, custom);

        // Assert — must be yellow, not red; the yellow zone (12–20) is occupied
        Assert.Equal("yellow", result);
        Assert.NotEqual("red", result);
    }

    // ── Async: GetSettingsAsync returns defaults when no DB row ───────────────

    [Fact]
    public async Task GoNoGoService_GetSettingsAsync_NoRowInDb_ReturnsDefaults()
    {
        // Arrange
        var factory = CreateFactory(nameof(GoNoGoService_GetSettingsAsync_NoRowInDb_ReturnsDefaults));
        var sut = new GoNoGoService(factory);

        // Act — no row has been saved for "user-x"
        var result = await sut.GetSettingsAsync("user-x");

        // Assert — returns a default GoNoGoSettings object (not null, sane defaults)
        Assert.NotNull(result);
        Assert.Equal(15, result.WindRedKt);    // default
        Assert.Equal(10, result.WindYellowKt); // default
        Assert.Equal(3,  result.VisRedKm);     // default
        Assert.Equal(5,  result.VisYellowKm);  // default
        Assert.Equal(500, result.CapeRedJkg);  // default
        Assert.Equal(300, result.CapeYellowJkg); // default
    }

    // ── Async: SaveSettingsAsync persists and can be retrieved ────────────────

    [Fact]
    public async Task GoNoGoService_SaveSettingsAsync_PersistsAndCanBeRetrieved()
    {
        // Arrange
        var factory = CreateFactory(nameof(GoNoGoService_SaveSettingsAsync_PersistsAndCanBeRetrieved));
        var sut = new GoNoGoService(factory);
        var settings = new GoNoGoSettings
        {
            WindYellowKt  = 8,
            WindRedKt     = 18,
            VisYellowKm   = 6,
            VisRedKm      = 2,
            CapeYellowJkg = 250,
            CapeRedJkg    = 600
        };

        // Act
        await sut.SaveSettingsAsync(settings, "user-save-test");
        var loaded = await sut.GetSettingsAsync("user-save-test");

        // Assert — all fields must round-trip correctly
        Assert.Equal(8,   loaded.WindYellowKt);
        Assert.Equal(18,  loaded.WindRedKt);
        Assert.Equal(6,   loaded.VisYellowKm);
        Assert.Equal(2,   loaded.VisRedKm);
        Assert.Equal(250, loaded.CapeYellowJkg);
        Assert.Equal(600, loaded.CapeRedJkg);
    }

    // ── Consistency: both Compute overloads agree ─────────────────────────────

    /// <summary>
    ///     The <c>Compute(FlightPreparation, GoNoGoSettings)</c> overload must always
    ///     return the same value as <c>Compute(windKt, visKm, capeJkg, GoNoGoSettings)</c>
    ///     when given the equivalent raw values from the flight preparation.
    /// </summary>
    [Theory]
    [InlineData(5.0,  10.0, 50.0,  "green")]
    [InlineData(10.0, null, null,   "yellow")]
    [InlineData(15.0, null, null,   "red")]
    [InlineData(null, 2.0,  null,   "red")]
    [InlineData(null, null, 500.0,  "red")]
    public void FlightPreparation_GoNoGo_MatchesGoNoGoServiceCompute_WithDefaultSettings(
        double? wind, double? vis, double? cape, string expectedResult)
    {
        // Arrange
        var sut      = new GoNoGoService(null!);
        var settings = new GoNoGoSettings(); // default thresholds
        var fp       = FpWith(wind, vis, cape);

        // Act — both overloads
        var fromFp  = sut.Compute(fp, settings);
        var fromRaw = sut.Compute(fp.SurfaceWindSpeedKt, fp.ZichtbaarheidKm, fp.CapeJkg, settings);

        // Assert — both overloads agree with each other and with the expected value
        Assert.Equal(expectedResult, fromFp);
        Assert.Equal(expectedResult, fromRaw);
    }
}
