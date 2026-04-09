using FlightPrep.Domain.Services;

namespace FlightPrep.Services;

/// <summary>
///     Computes sunrise and sunset times using the NOAA solar calculation algorithm.
///     Returns UTC times. Matches the NOAA Solar Calculator spreadsheet to within ~1 minute,
///     which aligns with Belgian AIP published tables.
/// </summary>
internal class SunriseService : ISunriseService
{
    public (TimeOnly Sunrise, TimeOnly Sunset) Calculate(DateOnly date, double latDeg, double lonDeg)
    {
        // Julian date
        var jd = ToJulianDate(date);

        // Julian century from J2000.0
        var t = (jd - 2451545.0) / 36525.0;

        // Geometric mean longitude of the sun (degrees)
        var l0 = (280.46646 + (t * (36000.76983 + (t * 0.0003032)))) % 360.0;

        // Geometric mean anomaly of the sun (degrees)
        var m = 357.52911 + (t * (35999.05029 - (0.0001537 * t)));
        var mRad = ToRad(m);

        // Orbital eccentricity of Earth (dimensionless, ~0.0167)
        var e = 0.016708634 - (t * (0.000042037 + (0.0000001267 * t)));

        // Equation of centre
        var c = (Math.Sin(mRad) * (1.914602 - (t * (0.004817 + (0.000014 * t)))))
                + (Math.Sin(2 * mRad) * (0.019993 - (0.000101 * t)))
                + (Math.Sin(3 * mRad) * 0.000289);

        // Sun's true longitude → apparent longitude
        var sunLon = l0 + c;
        var omega = 125.04 - (1934.136 * t);
        var lambdaRad = ToRad(sunLon - 0.00569 - (0.00478 * Math.Sin(ToRad(omega))));

        // Mean obliquity of the ecliptic (degrees)
        var eps0 = 23.0 + ((26.0 + ((21.448 - (t * (46.8150 + (t * (0.00059 - (t * 0.001813)))))) / 60.0)) / 60.0);

        // Corrected obliquity
        var epsCorr = eps0 + (0.00256 * Math.Cos(ToRad(omega)));
        var epsRad = ToRad(epsCorr);

        // Sun's declination
        var declRad = Math.Asin(Math.Sin(epsRad) * Math.Sin(lambdaRad));

        // Equation of time (minutes) — NOAA formula using orbital eccentricity e
        var y = Math.Tan(epsRad / 2) * Math.Tan(epsRad / 2);
        var l0Rad = ToRad(l0);
        var eot = 4.0 * ToDeg(
            (y * Math.Sin(2 * l0Rad))
            - (2 * e * Math.Sin(mRad))
            + (4 * e * y * Math.Sin(mRad) * Math.Cos(2 * l0Rad))
            - (0.5 * y * y * Math.Sin(4 * l0Rad))
            - (1.25 * e * e * Math.Sin(2 * mRad)));

        // Hour angle for sunrise (90.833° = geometric horizon + 0.833° refraction/disc)
        var latRad = ToRad(latDeg);
        var cosHa = (Math.Cos(ToRad(90.833)) - (Math.Sin(latRad) * Math.Sin(declRad)))
                    / (Math.Cos(latRad) * Math.Cos(declRad));

        // Clamp to [-1,1] to handle polar day/night
        cosHa = Math.Max(-1.0, Math.Min(1.0, cosHa));
        var haDeg = ToDeg(Math.Acos(cosHa));

        // Solar noon in minutes past midnight UTC
        var solarNoonMinUtc = 720.0 - (4.0 * lonDeg) - eot;

        var sunriseMinUtc = solarNoonMinUtc - (haDeg * 4.0);
        var sunsetMinUtc = solarNoonMinUtc + (haDeg * 4.0);

        return (MinutesToTimeOnly(sunriseMinUtc), MinutesToTimeOnly(sunsetMinUtc));
    }

    private static double ToJulianDate(DateOnly date)
    {
        var y = date.Year;
        var m = date.Month;
        var d = date.Day;
        if (m <= 2)
        {
            y--;
            m += 12;
        }

        var a = y / 100;
        var b = 2 - a + (a / 4);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    private static TimeOnly MinutesToTimeOnly(double totalMinutes)
    {
        totalMinutes = ((totalMinutes % 1440) + 1440) % 1440;
        var h = (int)(totalMinutes / 60) % 24;
        var min = (int)(totalMinutes % 60);
        return new TimeOnly(h, min);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
}
