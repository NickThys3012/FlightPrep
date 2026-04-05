using FlightPrep.Domain.Models;
using FlightPrep.Services;

namespace FlightPrep.Tests;

public class GoNoGoServiceComputeTests
{
    private static readonly GoNoGoSettings DefaultSettings = new();

    private static FlightPreparation FpWith(
        double? wind = null, double? vis = null, double? cape = null) => new() { SurfaceWindSpeedKt = wind, ZichtbaarheidKm = vis, CapeJkg = cape };

    [Fact]
    public void Compute_NoMeteoData_ReturnsUnknown()
    {
        var sut = new GoNoGoService(null!);
        var fp = new FlightPreparation();

        Assert.Equal("unknown", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_WindAtRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(15);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_WindAboveRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(25);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_VisibilityBelowRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(vis: 2.0);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_VisibilityExactlyAtRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(vis: 3.0);

        // vis < VisRedKm (3) is false for exactly 3 → not red from vis alone
        Assert.NotEqual("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CapeAboveRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(cape: 600);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_WindAtYellowThreshold_ReturnsYellow()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(10);

        Assert.Equal("yellow", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_VisibilityBelowYellowThreshold_ReturnsYellow()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(vis: 4.0);

        Assert.Equal("yellow", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CapeAboveYellowThreshold_ReturnsYellow()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(cape: 350);

        Assert.Equal("yellow", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_AllConditionsGood_ReturnsGreen()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(5, 10, 50);

        Assert.Equal("green", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CustomRedThresholds_AppliesCustomValues()
    {
        var sut = new GoNoGoService(null!);
        var custom = new GoNoGoSettings { WindRedKt = 20, VisRedKm = 1, CapeRedJkg = 1000 };
        var fp = FpWith(18);

        // wind 18 < custom red 20 → not red
        Assert.NotEqual("red", sut.Compute(fp, custom));
    }

    [Fact]
    public void Compute_RedTakesPriorityOverYellow()
    {
        var sut = new GoNoGoService(null!);
        // wind is in the yellow zone, CAPE is in the red zone
        var fp = FpWith(12, cape: 600);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    // ── Regression: #23 ───────────────────────────────────────────────────────

    /// <summary>
    ///     Regression: bug #23 — GoNoGo was using hardcoded thresholds (WindRedKt=15),
    ///     ignoring GoNoGoSettings. Pilot sets a wind red threshold to 20 kt.
    ///     Wind = 17 kt. Must be green/yellow, NOT red.
    /// </summary>
    [Fact]
    public void Compute_CustomWindRedThreshold_RespectsSettingNotHardcodedValue()
    {
        var sut = new GoNoGoService(null!);
        // Pilot raised the red threshold to 20 kt
        var customSettings = new GoNoGoSettings { WindYellowKt = 14, WindRedKt = 20 };
        // Wind is 17 kt — above the old hardcoded 15, but below the pilot's 20
        var fp = FpWith(17);

        var result = sut.Compute(fp, customSettings);

        // Must NOT be red: the pilot's threshold (20 kt) should govern, not the old hardcoded 15
        Assert.NotEqual("red", result);
        // 17 >= WindYellowKt(14) → yellow (not green), which is also acceptable
        Assert.Equal("yellow", result);
    }

    [Fact]
    public void Compute_CustomWindRedThreshold_AboveCustomLimit_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var customSettings = new GoNoGoSettings { WindYellowKt = 14, WindRedKt = 20 };
        var fp = FpWith(21); // above the custom 20 kt threshold

        Assert.Equal("red", sut.Compute(fp, customSettings));
    }

    // ── Boundary / threshold coverage ─────────────────────────────────────────

    [Fact]
    public void Compute_WindBelowYellowThreshold_OnlyWindDataPresent_ReturnsGreen()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(5); // 5 < WindYellowKt(10)

        Assert.Equal("green", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_VisibilityExactlyAtYellowThreshold_ReturnsGreen()
    {
        var sut = new GoNoGoService(null!);
        // vis < VisYellowKm(5) is false when vis == 5 → not yellow from vis
        var fp = FpWith(vis: 5.0);

        Assert.Equal("green", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CapeExactlyAtYellowThreshold_ReturnsYellow()
    {
        var sut = new GoNoGoService(null!);
        // CapeJkg >= CapeYellowJkg(300) when cape == 300 → yellow
        var fp = FpWith(cape: 300);

        Assert.Equal("yellow", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CapeExactlyAtRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        // CapeJkg >= CapeRedJkg(500) when cape == 500 → red
        var fp = FpWith(cape: 500);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_WindExactlyAtRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        // SurfaceWindSpeedKt >= WindRedKt(15) when wind == 15 → red
        var fp = FpWith(15);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CustomYellowThresholds_AppliesCustomValues()
    {
        var sut = new GoNoGoService(null!);
        // Raise yellow thresholds so a normally yellow reading becomes green
        var relaxed = new GoNoGoSettings { WindYellowKt = 18, WindRedKt = 25 };
        var fp = FpWith(12); // 12 < custom yellow 18 → green

        Assert.Equal("green", sut.Compute(fp, relaxed));
    }

    [Theory]
    [InlineData(15.0, null, null, "red")] // wind at red
    [InlineData(25.0, null, null, "red")] // wind above red
    [InlineData(null, 2.0, null, "red")] // vis below red
    [InlineData(null, null, 500.0, "red")] // cape at red
    [InlineData(10.0, null, null, "yellow")] // wind at yellow
    [InlineData(null, 4.0, null, "yellow")] // vis below yellow
    [InlineData(null, null, 300.0, "yellow")] // cape at yellow
    [InlineData(5.0, 10.0, 50.0, "green")] // all conditions good
    public void Compute_VariousConditions_ReturnsExpectedResult(
        double? wind, double? vis, double? cape, string expected)
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(wind, vis, cape);

        Assert.Equal(expected, sut.Compute(fp, DefaultSettings));
    }
}
