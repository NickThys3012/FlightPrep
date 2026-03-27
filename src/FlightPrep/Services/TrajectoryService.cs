using FlightPrep.Models;
using FlightPrep.Models.Trajectory;

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
            .Where(w => w.DirectionDeg != null && w.SpeedKt != null && w.SpeedKt > 0)
            .OrderBy(w => w.AltitudeFt)
            .ToList();

        var result = new List<SimulatedTrajectory>();
        int colorIndex = 0;

        foreach (var wind in valid)
        {
            double bearing = (wind.DirectionDeg!.Value + 180) % 360;
            double lat = launchLat;
            double lon = launchLon;

            int steps = durationMinutes / stepMinutes;
            var points = new List<TrajectoryPoint> { new(lat, lon) };

            for (int i = 0; i < steps; i++)
            {
                double distanceM = wind.SpeedKt!.Value * (stepMinutes / 60.0) * 1852.0;
                (lat, lon) = HaversineDestination(lat, lon, bearing, distanceM);
                points.Add(new TrajectoryPoint(lat, lon));
            }

            result.Add(new SimulatedTrajectory(
                AltitudeFt: wind.AltitudeFt,
                Color: Palette[colorIndex % Palette.Length],
                Points: points,
                DataSource: dataSource,
                SimulatedAt: DateTime.UtcNow,
                DurationMinutes: durationMinutes
            ));

            colorIndex++;
        }

        return result.OrderBy(t => t.AltitudeFt).ToList();
    }

    private static (double Lat, double Lon) HaversineDestination(
        double lat, double lon, double bearingDeg, double distanceM)
    {
        const double R = 6_371_000;
        double d  = distanceM / R;
        double p1 = lat * Math.PI / 180;
        double l1 = lon * Math.PI / 180;
        double b  = bearingDeg * Math.PI / 180;
        double p2 = Math.Asin(Math.Sin(p1) * Math.Cos(d)
                             + Math.Cos(p1) * Math.Sin(d) * Math.Cos(b));
        double l2 = l1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(p1),
                                      Math.Cos(d) - Math.Sin(p1) * Math.Sin(p2));
        return (p2 * 180 / Math.PI, l2 * 180 / Math.PI);
    }
}
