using FlightPrep.Models;
using FlightPrep.Models.Trajectory;

namespace FlightPrep.Services;

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
