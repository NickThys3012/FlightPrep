using FlightPrep.Domain.Models;
using FlightPrep.Domain.Models.Trajectory;

namespace FlightPrep.Domain.Services;

public interface ITrajectoryService
{
    List<SimulatedTrajectory> Compute(
        double launchLat,
        double launchLon,
        IEnumerable<WindLevel> windLevels,
        int durationMinutes,
        int stepMinutes = 5,
        TrajectoryDataSource dataSource = TrajectoryDataSource.Manual);
}
