namespace FlightPrep.Services;

public record TrackPoint(double Lat, double Lon, double AltM);

public class KmlService
{
    /// Parses a KML string and returns the first LineString's coordinates.
    /// Returns empty list on parse error.
    public List<TrackPoint> ParseCoordinates(string kml)
    {
        var result = new List<TrackPoint>();
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(kml);
            // KML uses namespace http://www.opengis.net/kml/2.2
            var ns = "http://www.opengis.net/kml/2.2";
            var coordText = doc.Descendants(System.Xml.Linq.XName.Get("coordinates", ns))
                               .FirstOrDefault()?.Value?.Trim();
            if (coordText == null) return result;
            foreach (var token in coordText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(',');
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                {
                    var alt = parts.Length >= 3 && double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0;
                    result.Add(new TrackPoint(lat, lon, alt));
                }
            }
        }
        catch { /* malformed KML - return empty */ }
        return result;
    }

    public (double MaxAltM, double MinAltM, double DistanceKm) ComputeStats(List<TrackPoint> pts)
    {
        if (pts.Count == 0) return (0, 0, 0);
        double maxA = pts.Max(p => p.AltM);
        double minA = pts.Min(p => p.AltM);
        double dist = 0;
        for (int i = 1; i < pts.Count; i++)
            dist += HaversineKm(pts[i-1].Lat, pts[i-1].Lon, pts[i].Lat, pts[i].Lon);
        return (maxA, minA, dist);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat/2)*Math.Sin(dLat/2) +
                Math.Cos(lat1*Math.PI/180)*Math.Cos(lat2*Math.PI/180)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }
}
