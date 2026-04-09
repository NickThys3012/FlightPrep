using FlightPrep.Domain.Models;
using FlightPrep.Domain.Models.Trajectory;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Services;

namespace FlightPrep.Services;

public class TrajectoryService : ITrajectoryService
{
    private static readonly string[] Palette =
    [
        "#2ecc71", "#3498db", "#9b59b6", "#f1c40f", "#e67e22", "#e74c3c", "#1abc9c"
    ];

    public List<SimulatedTrajectory> Compute(
        double launchLat,
        double launchLon,
        IEnumerable<WindLevel> windLevels,
        int durationMinutes,
        int stepMinutes = 5,
        TrajectoryDataSource dataSource = TrajectoryDataSource.Manual)
    {
        ArgumentNullException.ThrowIfNull(windLevels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepMinutes);

        var valid = windLevels
            .Where(w => w is { DirectionDeg: not null, SpeedKt: > 0 })
            .OrderBy(w => w.AltitudeFt)
            .ToList();

        var result = new List<SimulatedTrajectory>();
        var colorIndex = 0;

        foreach (var wind in valid)
        {
            double bearing = (wind.DirectionDeg!.Value + 180) % 360;
            var lat = launchLat;
            var lon = launchLon;

            var steps = durationMinutes / stepMinutes;
            var points = new List<TrajectoryPoint> { new(lat, lon) };

            for (var i = 0; i < steps; i++)
            {
                var distanceM = wind.SpeedKt!.Value * (stepMinutes / 60.0) * 1852.0;
                (lat, lon) = TrajectoryMath.HaversineDestination(lat, lon, bearing, distanceM);
                points.Add(new TrajectoryPoint(lat, lon));
            }

            result.Add(new SimulatedTrajectory(
                wind.AltitudeFt,
                Palette[colorIndex % Palette.Length],
                points,
                dataSource,
                DateTime.UtcNow,
                durationMinutes
            ));

            colorIndex++;
        }

        return result.OrderBy(t => t.AltitudeFt).ToList();
    }
}
