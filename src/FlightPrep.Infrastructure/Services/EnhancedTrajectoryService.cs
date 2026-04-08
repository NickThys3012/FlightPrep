using FlightPrep.Domain.Models;
using FlightPrep.Domain.Models.Trajectory;
using FlightPrep.Domain.Services;

namespace FlightPrep.Infrastructure.Services;

public class EnhancedTrajectoryService(
    IWeatherFetchService weatherService,
    ILogger<EnhancedTrajectoryService> logger) : IEnhancedTrajectoryService
{
    private static readonly string[] ColorPalette =
        ["#2ecc71", "#3498db", "#9b59b6", "#f1c40f", "#e67e22", "#e74c3c", "#1abc9c"];

    public async Task<SimulatedTrajectory> ComputeAsync(
        double launchLat,
        double launchLon,
        DateTime launchTimeUtc,
        double ascentRateMs,
        int cruiseAltitudeFt,
        double descentRateMs,
        int totalDurationMinutes,
        int stepSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ascentRateMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(descentRateMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cruiseAltitudeFt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalDurationMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepSeconds);

        var toUtc = launchTimeUtc.AddMinutes(totalDurationMinutes);
        var snapshots = await weatherService.FetchWindTimeSeriesAsync(launchLat, launchLon, launchTimeUtc, toUtc);

        if (snapshots.Count == 0)
        {
            logger.LogWarning("EnhancedTrajectoryService: no wind data for {Lat},{Lon} at {Time}", launchLat, launchLon, launchTimeUtc);
            return new SimulatedTrajectory(
                cruiseAltitudeFt,
                "#e74c3c",
                [],
                TrajectoryDataSource.Hysplit,
                DateTime.UtcNow,
                totalDurationMinutes,
                ascentRateMs,
                descentRateMs);
        }

        // Convert cruise altitude from ft to metres for calculation
        var cruiseAltM = cruiseAltitudeFt * 0.3048;

        // Phase durations in seconds
        var ascentDurationS = cruiseAltM / ascentRateMs;
        var totalDurationS = totalDurationMinutes * 60.0;
        var descentDurationS = cruiseAltM / descentRateMs;
        var cruiseDurationS = totalDurationS - ascentDurationS - descentDurationS;
        // If ascent + descent > total time, cap at total time (no cruise phase)
        if (cruiseDurationS < 0)
        {
            // Scale ascent/descent proportionally to fit in totalDurationS
            var totalVerticalS = ascentDurationS + descentDurationS;
            var scale = totalDurationS / totalVerticalS;
            ascentDurationS *= scale;
            cruiseDurationS = 0;
        }

        var ascentEndS = ascentDurationS;
        var cruiseEndS = ascentEndS + cruiseDurationS;

        var points = new List<TrajectoryPoint>();
        var lat = launchLat;
        var lon = launchLon;

        for (double elapsed = 0; elapsed <= totalDurationS; elapsed += stepSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Current altitude
            double currentAltM;
            if (elapsed <= ascentEndS)
            {
                currentAltM = ascentRateMs * elapsed;
            }
            else if (elapsed <= cruiseEndS)
            {
                currentAltM = cruiseAltM;
            }
            else
            {
                currentAltM = cruiseAltM - (descentRateMs * (elapsed - cruiseEndS));
            }

            currentAltM = Math.Max(0, currentAltM);
            var currentAltFt = currentAltM / 0.3048;

            // Current wall-clock time
            var currentTime = launchTimeUtc.AddSeconds(elapsed);

            // Interpolate wind: time interpolation between two hourly snapshots
            var (snapshot1, snapshot2, timeFraction) = InterpolateSnapshots(snapshots, currentTime);
            if (snapshot1 == null)
            {
                break;
            }

            // Interpolate wind: altitude interpolation between two pressure levels
            // snapshot2 is always non-null when snapshot1 is non-null (see InterpolateSnapshots)
            var (speedKt, dirDeg) = InterpolateWind(snapshot1, snapshot2!, timeFraction, currentAltFt);

            // Move
            if (speedKt > 0)
            {
                var bearing = (dirDeg + 180.0) % 360.0;
                var distanceM = speedKt * 1852.0 / 3600.0 * stepSeconds;
                (lat, lon) = TrajectoryMath.HaversineDestination(lat, lon, bearing, distanceM);
            }

            var elapsedMin = (int)Math.Round(elapsed / 60.0);
            points.Add(new TrajectoryPoint(lat, lon, currentAltFt, elapsedMin));
        }

        return new SimulatedTrajectory(
            cruiseAltitudeFt,
            "#e74c3c",
            points,
            TrajectoryDataSource.Hysplit,
            DateTime.UtcNow,
            totalDurationMinutes,
            ascentRateMs,
            descentRateMs);
    }

    public async Task<List<SimulatedTrajectory>> ComputeMultipleAsync(
        double launchLat,
        double launchLon,
        DateTime launchTimeUtc,
        double ascentRateMs,
        IEnumerable<int> cruiseAltitudesFt,
        double descentRateMs,
        int totalDurationMinutes,
        int stepSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        var altitudes = cruiseAltitudesFt.Distinct().OrderBy(a => a).Take(5).ToList();
        if (altitudes.Count == 0)
        {
            return [];
        }

        var tasks = altitudes.Select(alt => ComputeAsync(
            launchLat, launchLon, launchTimeUtc,
            ascentRateMs, alt, descentRateMs,
            totalDurationMinutes, stepSeconds, cancellationToken));

        var results = await Task.WhenAll(tasks);

        return results
            .Select((traj, idx) => traj with { Color = ColorPalette[idx % ColorPalette.Length] })
            .ToList();
    }

    private static (WindSnapshot? s1, WindSnapshot? s2, double fraction) InterpolateSnapshots(
        List<WindSnapshot> snapshots, DateTime time)
    {
        if (snapshots.Count == 0)
        {
            return (null, null, 0);
        }

        // Find the two snapshots bracketing `time`
        WindSnapshot? s1 = null, s2 = null;
        for (var i = 0; i < snapshots.Count - 1; i++)
        {
            if (snapshots[i].TimeUtc > time || snapshots[i + 1].TimeUtc <= time)
            {
                continue;
            }

            s1 = snapshots[i];
            s2 = snapshots[i + 1];
            break;
        }

        if (s1 == null)
        {
            // Before first or after last — use nearest
            if (time <= snapshots[0].TimeUtc)
            {
                return (snapshots[0], snapshots[0], 0);
            }

            var last = snapshots[^1];
            return (last, last, 0);
        }

        var totalSpan = (s2!.TimeUtc - s1.TimeUtc).TotalSeconds;
        var elapsedSpan = (time - s1.TimeUtc).TotalSeconds;
        var fraction = totalSpan > 0 ? elapsedSpan / totalSpan : 0;
        return (s1, s2, fraction);
    }

    private static (double SpeedKt, double DirDeg) InterpolateWind(
        WindSnapshot s1, WindSnapshot s2, double timeFraction, double altitudeFt)
    {
        // Altitude interpolation: find two bracketing pressure levels in s1
        var levels1 = s1.Levels.OrderBy(l => l.AltitudeFt).ToList();
        var levels2 = s2.Levels.OrderBy(l => l.AltitudeFt).ToList();

        var (speed1, dir1) = InterpolateAltitude(levels1, altitudeFt);
        var (speed2, dir2) = InterpolateAltitude(levels2, altitudeFt);

        // Time interpolation
        var speed = speed1 + ((speed2 - speed1) * timeFraction);
        var dir = LerpAngle(dir1, dir2, timeFraction);
        return (speed, dir);
    }

    private static (double SpeedKt, double DirDeg) InterpolateAltitude(
        List<WindLevel> levels, double altitudeFt)
    {
        if (levels.Count == 0)
        {
            return (0, 0);
        }

        // Below the lowest level
        if (altitudeFt <= levels[0].AltitudeFt)
        {
            return (levels[0].SpeedKt ?? 0, levels[0].DirectionDeg ?? 0);
        }

        // Above highest level
        if (altitudeFt >= levels[^1].AltitudeFt)
        {
            var top = levels[^1];
            return (top.SpeedKt ?? 0, top.DirectionDeg ?? 0);
        }

        // Find bracketing levels
        for (var i = 0; i < levels.Count - 1; i++)
        {
            var lo = levels[i];
            var hi = levels[i + 1];
            if (!(altitudeFt >= lo.AltitudeFt) || !(altitudeFt <= hi.AltitudeFt))
            {
                continue;
            }

            double span = hi.AltitudeFt - lo.AltitudeFt;
            var f = span > 0 ? (altitudeFt - lo.AltitudeFt) / span : 0;
            var speed = (lo.SpeedKt ?? 0) + (((hi.SpeedKt ?? 0) - (lo.SpeedKt ?? 0)) * f);
            var dir = LerpAngle(lo.DirectionDeg ?? 0, hi.DirectionDeg ?? 0, f);
            return (speed, dir);
        }

        return (0, 0);
    }

    /// <summary>Linearly interpolate between two angles (degrees), taking the short arc.</summary>
    private static double LerpAngle(double a, double b, double t)
    {
        var diff = ((b - a + 540) % 360) - 180;
        return (a + (diff * t) + 360) % 360;
    }
}
