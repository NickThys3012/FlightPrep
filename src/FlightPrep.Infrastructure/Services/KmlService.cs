using FlightPrep.Domain.Services;
using System.Globalization;
using System.Xml.Linq;

namespace FlightPrep.Infrastructure.Services;

// TrackPoint is defined in FlightPrep.Domain/Services/TrackPoint.cs

public class KmlService : IKmlService
{
    /// Parses a KML string and returns the first LineString's coordinates.
    /// Returns an empty list on parse error.
    public List<TrackPoint> ParseCoordinates(string kml)
    {
        var result = new List<TrackPoint>();
        try
        {
            var doc = XDocument.Parse(kml);
            // KML uses namespace http://www.opengis.net/kml/2.2
            const string ns = "http://www.opengis.net/kml/2.2";
            var coordText = doc.Descendants(XName.Get("coordinates", ns))
                .FirstOrDefault()?.Value.Trim();
            if (coordText == null)
            {
                return result;
            }

            foreach (var token in coordText.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(',');
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                {
                    var alt = parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0;
                    result.Add(new TrackPoint(lat, lon, alt));
                }
            }
        }
        catch
        {
            /* malformed KML - return empty */
        }

        return result;
    }

    public (double MaxAltM, double MinAltM, double DistanceKm) ComputeStats(List<TrackPoint> pts)
    {
        if (pts.Count == 0)
        {
            return (0, 0, 0);
        }

        var maxA = pts.Max(p => p.AltM);
        var minA = pts.Min(p => p.AltM);
        double dist = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            dist += HaversineKm(pts[i - 1].Lat, pts[i - 1].Lon, pts[i].Lat, pts[i].Lon);
        }

        return (maxA, minA, dist);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                (Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
