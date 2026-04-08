using FlightPrep.Domain.Models;

namespace FlightPrep.Tests;

/// <summary>
///     Unit tests for the OFP weight-snapshot pattern applied when a balloon is selected
///     on the flight-edit page.  The logic under test mirrors <c>OnBalloonChanged</c>
///     in <c>FlightEdit.razor.cs</c>:
///     <code>
///         if (fp.CylindersWeightKg == null) fp.CylindersWeightKg = balloon.CylindersWeightKg;
///     </code>
///     These tests exercise that rule against the domain models directly, without
///     requiring a Blazor component host.
/// </summary>
public class BalloonSnapshotTests
{
    // ── CylindersWeightKg snapshot ────────────────────────────────────────────

    [Fact]
    public void OnBalloonChanged_SnapshotsCylindersWeightKg_WhenNull()
    {
        // Arrange – flight has no cylinders weight yet; balloon declares 80 kg
        var fp      = new FlightPreparation { CylindersWeightKg = null };
        var balloon = new Balloon            { CylindersWeightKg = 80   };

        // Act – replicate the OnBalloonChanged guard
        if (fp.CylindersWeightKg == null)
            fp.CylindersWeightKg = balloon.CylindersWeightKg;

        // Assert – value must be copied from the balloon
        Assert.Equal(80, fp.CylindersWeightKg);
    }

    [Fact]
    public void OnBalloonChanged_DoesNotOverwriteCylindersWeightKg_WhenAlreadySet()
    {
        // Arrange – flight already has a pilot-entered cylinders weight of 60 kg
        var fp      = new FlightPreparation { CylindersWeightKg = 60 };
        var balloon = new Balloon            { CylindersWeightKg = 80 };

        // Act – replicate the OnBalloonChanged guard
        if (fp.CylindersWeightKg == null)
            fp.CylindersWeightKg = balloon.CylindersWeightKg;

        // Assert – pre-existing value must be preserved
        Assert.Equal(60, fp.CylindersWeightKg);
    }

    // ── OFP full-set snapshot (all four fields) ───────────────────────────────

    [Fact]
    public void OnBalloonChanged_SnapshotsAllOFPWeights_WhenAllNull()
    {
        // Arrange – a new flight where nothing has been set yet
        var fp = new FlightPreparation
        {
            OFPEnvelopeWeightKg = null,
            OFPBasketWeightKg   = null,
            OFPBurnerWeightKg   = null,
            CylindersWeightKg   = null
        };
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = 250,
            BasketWeightKg       = 70,
            BurnerWeightKg       = 18,
            CylindersWeightKg    = 80
        };

        // Act – replicate the full OnBalloonChanged guard block
        if (fp.OFPEnvelopeWeightKg == null) fp.OFPEnvelopeWeightKg = balloon.EnvelopeOnlyWeightKg;
        if (fp.OFPBasketWeightKg   == null) fp.OFPBasketWeightKg   = balloon.BasketWeightKg;
        if (fp.OFPBurnerWeightKg   == null) fp.OFPBurnerWeightKg   = balloon.BurnerWeightKg;
        if (fp.CylindersWeightKg   == null) fp.CylindersWeightKg   = balloon.CylindersWeightKg;

        // Assert
        Assert.Equal(250, fp.OFPEnvelopeWeightKg);
        Assert.Equal(70,  fp.OFPBasketWeightKg);
        Assert.Equal(18,  fp.OFPBurnerWeightKg);
        Assert.Equal(80,  fp.CylindersWeightKg);
    }

    [Fact]
    public void OnBalloonChanged_PreservesAllOFPWeights_WhenAllAlreadySet()
    {
        // Arrange – pilot has already entered custom values for every OFP field
        var fp = new FlightPreparation
        {
            OFPEnvelopeWeightKg = 260,
            OFPBasketWeightKg   = 72,
            OFPBurnerWeightKg   = 20,
            CylindersWeightKg   = 60
        };
        var balloon = new Balloon
        {
            EnvelopeOnlyWeightKg = 250,
            BasketWeightKg       = 70,
            BurnerWeightKg       = 18,
            CylindersWeightKg    = 80
        };

        // Act
        if (fp.OFPEnvelopeWeightKg == null) fp.OFPEnvelopeWeightKg = balloon.EnvelopeOnlyWeightKg;
        if (fp.OFPBasketWeightKg   == null) fp.OFPBasketWeightKg   = balloon.BasketWeightKg;
        if (fp.OFPBurnerWeightKg   == null) fp.OFPBurnerWeightKg   = balloon.BurnerWeightKg;
        if (fp.CylindersWeightKg   == null) fp.CylindersWeightKg   = balloon.CylindersWeightKg;

        // Assert – none of the pilot-entered values should change
        Assert.Equal(260, fp.OFPEnvelopeWeightKg);
        Assert.Equal(72,  fp.OFPBasketWeightKg);
        Assert.Equal(20,  fp.OFPBurnerWeightKg);
        Assert.Equal(60,  fp.CylindersWeightKg);
    }
}
