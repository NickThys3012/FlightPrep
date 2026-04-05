namespace FlightPrep.Infrastructure.Services;

public static class TrajectoryMath
{
    /// <summary>
    ///     Computes a destination point given a start point, bearing, and distance.
    ///     Wind direction convention: wind FROM a direction. Balloon bearing = (windDir + 180) % 360.
    /// </summary>
    public static (double Lat, double Lon) HaversineDestination(
        double lat, double lon, double bearingDeg, double distanceM)
    {
        // ReSharper disable once InconsistentNaming
        const double R = 6_371_000;
        var d = distanceM / R;
        var p1 = lat * Math.PI / 180;
        var l1 = lon * Math.PI / 180;
        var b = bearingDeg * Math.PI / 180;
        var p2 = Math.Asin((Math.Sin(p1) * Math.Cos(d))
                           + (Math.Cos(p1) * Math.Sin(d) * Math.Cos(b)));
        var l2 = l1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(p1),
            Math.Cos(d) - (Math.Sin(p1) * Math.Sin(p2)));
        return (p2 * 180 / Math.PI, l2 * 180 / Math.PI);
    }
}
