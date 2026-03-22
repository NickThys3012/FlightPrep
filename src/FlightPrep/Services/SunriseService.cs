namespace FlightPrep.Services;

/// <summary>
/// Computes sunrise and sunset times using the NOAA solar calculation algorithm.
/// Returns UTC times.
/// </summary>
public class SunriseService
{
    public (TimeOnly Sunrise, TimeOnly Sunset) Calculate(DateOnly date, double latDeg, double lonDeg)
    {
        // Julian date
        double jd = ToJulianDate(date);

        // Julian century
        double t = (jd - 2451545.0) / 36525.0;

        // Geometric mean longitude of the sun (degrees)
        double l0 = (280.46646 + t * (36000.76983 + t * 0.0003032)) % 360.0;

        // Geometric mean anomaly of the sun (degrees)
        double m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        double mRad = ToRad(m);

        // Equation of centre
        double c = Math.Sin(mRad) * (1.914602 - t * (0.004817 + 0.000014 * t))
                 + Math.Sin(2 * mRad) * (0.019993 - 0.000101 * t)
                 + Math.Sin(3 * mRad) * 0.000289;

        // Sun's true longitude
        double sunLon = l0 + c;

        // Sun's apparent longitude
        double omega = 125.04 - 1934.136 * t;
        double lambdaRad = ToRad(sunLon - 0.00569 - 0.00478 * Math.Sin(ToRad(omega)));

        // Mean obliquity of the ecliptic (degrees)
        double eps0 = 23.0 + (26.0 + (21.448 - t * (46.8150 + t * (0.00059 - t * 0.001813))) / 60.0) / 60.0;

        // Corrected obliquity
        double epsCorr = eps0 + 0.00256 * Math.Cos(ToRad(omega));
        double epsRad = ToRad(epsCorr);

        // Sun's declination
        double declRad = Math.Asin(Math.Sin(epsRad) * Math.Sin(lambdaRad));

        // Equation of time (minutes)
        double y = Math.Tan(epsRad / 2) * Math.Tan(epsRad / 2);
        double l0Rad = ToRad(l0);
        double eot = 4.0 * ToDeg(
            y * Math.Sin(2 * l0Rad)
            - 2 * ToRad(c) * Math.Sin(mRad)   // eccentricity term (approximation)
            + 4 * ToRad(c) * y * Math.Sin(mRad) * Math.Cos(2 * l0Rad)
            - 0.5 * y * y * Math.Sin(4 * l0Rad)
            - 1.25 * ToRad(c) * ToRad(c) * Math.Sin(2 * mRad));

        // Hour angle for sunrise (degrees)
        double latRad = ToRad(latDeg);
        double cosHA = (Math.Cos(ToRad(90.833)) - Math.Sin(latRad) * Math.Sin(declRad))
                       / (Math.Cos(latRad) * Math.Cos(declRad));

        // Clamp to [-1,1] to handle polar day/night
        cosHA = Math.Max(-1.0, Math.Min(1.0, cosHA));
        double haRad = Math.Acos(cosHA);
        double haDeg = ToDeg(haRad);

        // Solar noon in minutes past midnight UTC
        double solarNoonMinUtc = 720.0 - 4.0 * lonDeg - eot;

        double sunriseMinUtc = solarNoonMinUtc - haDeg * 4.0;
        double sunsetMinUtc  = solarNoonMinUtc + haDeg * 4.0;

        return (MinutesToTimeOnly(sunriseMinUtc), MinutesToTimeOnly(sunsetMinUtc));
    }

    private static double ToJulianDate(DateOnly date)
    {
        int y = date.Year;
        int m = date.Month;
        int d = date.Day;
        if (m <= 2) { y--; m += 12; }
        int a = y / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    private static TimeOnly MinutesToTimeOnly(double totalMinutes)
    {
        // Wrap to [0, 1440)
        totalMinutes = ((totalMinutes % 1440) + 1440) % 1440;
        int h = (int)(totalMinutes / 60) % 24;
        int min = (int)(totalMinutes % 60);
        return new TimeOnly(h, min);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
}
