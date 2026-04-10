using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FlightPrep.Components;

public partial class FlightPassengerList:ComponentBase
{
    /// <summary>The current flight being edited. Mutations are applied directly to this reference.</summary>
    [Parameter]
    public FlightPreparation Fp { get; set; } = null!;

    /// <summary>The currently selected pilot (for the read-only pilot weight row).</summary>
    [Parameter]
    public Pilot? CurrentPilot { get; set; }

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Fp);
    }

    private void AddPassenger()
    {
        Fp.Passengers.Add(new Passenger { Order = Fp.Passengers.Count });
    }

    private void RemovePassenger(int index)
    {
        if (index >= 0 && index < Fp.Passengers.Count)
            Fp.Passengers.RemoveAt(index);
    }

    /// <summary>Computes the total weight: envelope + pilot + all passengers.</summary>
    private double CalcTotalWeight()
    {
        return (Fp.EnvelopeWeightKg ?? 0)
               + (CurrentPilot?.WeightKg ?? 0)
               + (Fp.Passengers.Sum(p => p.WeightKg));
    }

    private LiftResult? _liftResult;

    private bool CanCalculateLift =>
        Fp.Balloon is { VolumeM3: > 0, InternalEnvelopeTempC: >= 0 and <= 100 } &&
        Fp.Location?.ElevationM.HasValue == true &&
        Fp is { TemperatureC: not null, MaxAltitudeFt: not null } &&
        Fp.MaxAltitudeFt * 0.3048 > Fp.Location!.ElevationM;

    /// <summary>
    ///     Calculates total lift using the ISA formula (Cameron Flight Manual, Appendix 2)
    ///     and writes the result to Fp.TotaalLiftKg.
    /// </summary>
    private void BerekenLift()
    {
        if (!CanCalculateLift) return;
        var a = Fp.MaxAltitudeFt!.Value * 0.3048;
        var eg = Fp.Location!.ElevationM!.Value;
        var tg = Fp.TemperatureC!.Value;
        var ti = Fp.Balloon!.InternalEnvelopeTempC!.Value;
        var v = Fp.Balloon!.VolumeM3!.Value;
        _liftResult = LiftCalculator.Calculate(a, eg, tg, ti, v);
        Fp.TotaalLiftKg = _liftResult.TotalLiftKg;
    }
}
