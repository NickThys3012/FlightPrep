namespace FlightPrep.Services;

internal static class TrajectoryMath
{
    /// <summary>
    /// Computes a destination point given a start point, bearing, and distance.
    /// Wind direction convention: wind FROM direction. Balloon bearing = (windDir + 180) % 360.
    /// </summary>
    internal static (double Lat, double Lon) HaversineDestination(
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
