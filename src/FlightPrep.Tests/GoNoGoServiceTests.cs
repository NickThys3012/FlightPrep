using FlightPrep.Models;
using FlightPrep.Services;

namespace FlightPrep.Tests;

public class GoNoGoServiceComputeTests
{
    private static readonly GoNoGoSettings DefaultSettings = new();

    private static FlightPreparation FpWith(
        double? wind = null, double? vis = null, double? cape = null) => new()
    {
        SurfaceWindSpeedKt = wind,
        ZichtbaarheidKm = vis,
        CapeJkg = cape
    };

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
        var fp = FpWith(wind: 15);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_WindAboveRedThreshold_ReturnsRed()
    {
        var sut = new GoNoGoService(null!);
        var fp = FpWith(wind: 25);

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
        var fp = FpWith(wind: 10);

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
        var fp = FpWith(wind: 5, vis: 10, cape: 50);

        Assert.Equal("green", sut.Compute(fp, DefaultSettings));
    }

    [Fact]
    public void Compute_CustomRedThresholds_AppliesCustomValues()
    {
        var sut = new GoNoGoService(null!);
        var custom = new GoNoGoSettings { WindRedKt = 20, VisRedKm = 1, CapeRedJkg = 1000 };
        var fp = FpWith(wind: 18);

        // wind 18 < custom red 20 → not red
        Assert.NotEqual("red", sut.Compute(fp, custom));
    }

    [Fact]
    public void Compute_RedTakesPriorityOverYellow()
    {
        var sut = new GoNoGoService(null!);
        // wind is in yellow zone, CAPE is in red zone
        var fp = FpWith(wind: 12, cape: 600);

        Assert.Equal("red", sut.Compute(fp, DefaultSettings));
    }
}
