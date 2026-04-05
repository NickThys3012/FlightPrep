using FlightPrep.Models;

namespace FlightPrep.Services;

/// <summary>
/// Computes derived flight-readiness values from a <see cref="FlightPreparation"/>
/// using the pilot-configured <see cref="GoNoGoSettings"/>.
/// Centralises the logic that was previously spread across entity computed properties
/// and direct callers.
/// </summary>
public class FlightAssessmentService(IGoNoGoService goNoGoSvc) : IFlightAssessmentService
{
    /// <summary>
    /// Computes the <see cref="FlightAssessment"/> for the given flight preparation.
    /// The <see cref="GoNoGoSettings"/> are loaded from the database on each call.
    /// </summary>
    public async Task<FlightAssessment> ComputeAsync(FlightPreparation fp, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(fp);

        var settings = await goNoGoSvc.GetSettingsAsync(userId);

        var totaalGewicht = (fp.EnvelopeWeightKg ?? 0)
                          + (fp.Pilot?.WeightKg ?? 0)
                          + fp.Passengers.Sum(p => p.WeightKg);

        var liftVoldoende = fp.TotaalLiftKg.HasValue
                         && fp.TotaalLiftKg.Value > totaalGewicht;

        var goNoGo = goNoGoSvc.Compute(
            fp.SurfaceWindSpeedKt,
            fp.ZichtbaarheidKm,
            fp.CapeJkg,
            settings);

        return new FlightAssessment(totaalGewicht, liftVoldoende, goNoGo);
    }

    /// <summary>
    /// Synchronous overload when the caller already holds the <see cref="GoNoGoSettings"/>.
    /// </summary>
    public FlightAssessment Compute(FlightPreparation fp, GoNoGoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fp);
        ArgumentNullException.ThrowIfNull(settings);

        var totaalGewicht = (fp.EnvelopeWeightKg ?? 0)
                          + (fp.Pilot?.WeightKg ?? 0)
                          + fp.Passengers.Sum(p => p.WeightKg);

        var liftVoldoende = fp.TotaalLiftKg.HasValue
                         && fp.TotaalLiftKg.Value > totaalGewicht;

        var goNoGo = goNoGoSvc.Compute(
            fp.SurfaceWindSpeedKt,
            fp.ZichtbaarheidKm,
            fp.CapeJkg,
            settings);

        return new FlightAssessment(totaalGewicht, liftVoldoende, goNoGo);
    }
}
