using FlightPrep.Models.Trajectory;

namespace FlightPrep.Services;

public interface IEnhancedTrajectoryService
{
    /// <summary>
    /// Computes a 3D time-varying balloon trajectory using hourly wind data at multiple pressure levels.
    /// </summary>
    Task<SimulatedTrajectory> ComputeAsync(
        double launchLat,
        double launchLon,
        DateTime launchTimeUtc,
        double ascentRateMs,
        int cruiseAltitudeFt,
        double descentRateMs,
        int totalDurationMinutes,
        int stepSeconds = 60,
        CancellationToken cancellationToken = default);
}
