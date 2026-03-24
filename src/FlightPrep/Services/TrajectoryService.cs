using System.Globalization;
using System.Text.Json;

namespace FlightPrep.Services;

public record WindAtLevel(int HpaLevel, int AltitudeM, double SpeedKmh, double DirectionDeg);
public record TrajectoryPoint(double Lat, double Lon);
public record PredictedTrajectory(int HpaLevel, int AltitudeM, List<TrajectoryPoint> Points, string Color);
public record TrajectoryResultEntry(int HpaLevel, int AltitudeM, double EndLat, double EndLon, double DistanceKm, double WindSpeedKmh, double WindDirDeg);

public class TrajectoryService(IHttpClientFactory httpFactory)
{
    // Key 9999 = surface (~10m / 33 ft), fetched via windspeed_10m
    private static readonly Dictionary<int, int> LevelAltitudes = new()
    {
        { 9999, 10 },
        { 1000, 110 }, { 975, 320 }, { 950, 540 }, { 925, 760 },
        { 850, 1500 }, { 800, 1950 }, { 750, 2450 }, { 700, 3000 },
        { 650, 3600 }, { 600, 4200 }, { 500, 5500 }
    };

    private static readonly string[] Colors =
        { "#e74c3c", "#3498db", "#2ecc71", "#f39c12", "#9b59b6", "#1abc9c", "#e67e22", "#16a085", "#8e44ad", "#d35400", "#2c3e50" };

    public static string LevelLabel(int hpa) => hpa == 9999 ? "Oppervlak" : $"{hpa} hPa";

    public async Task<List<PredictedTrajectory>> PredictAsync(
        double lat, double lon,
        DateTime flightTimeLocal,
        int[] hpaLevels,
        int durationMinutes)
    {
        var levels = hpaLevels.Where(l => LevelAltitudes.ContainsKey(l)).ToArray();
        if (levels.Length == 0) return new();

        // Pressure levels (exclude surface sentinel 9999)
        var pressureLevels = levels.Where(l => l != 9999).ToArray();
        bool includeSurface = levels.Contains(9999);

        var hourlyParams = new List<string>();
        if (includeSurface)
        {
            hourlyParams.Add("windspeed_10m");
            hourlyParams.Add("winddirection_10m");
        }
        foreach (var l in pressureLevels)
        {
            hourlyParams.Add($"windspeed_{l}hPa");
            hourlyParams.Add($"winddirection_{l}hPa");
        }

        var url = $"v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&hourly={string.Join(",", hourlyParams)}&forecast_days=3&timezone=Europe%2FBrussels";

        try
        {
            var client = httpFactory.CreateClient("openmeteo");
            var json = await client.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var hourly = doc.RootElement.GetProperty("hourly");

            var times = hourly.GetProperty("time").EnumerateArray()
                .Select(t => DateTime.Parse(t.GetString()!)).ToList();

            int idx = 0;
            double minDiff = double.MaxValue;
            for (int i = 0; i < times.Count; i++)
            {
                var diff = Math.Abs((times[i] - flightTimeLocal).TotalMinutes);
                if (diff < minDiff) { minDiff = diff; idx = i; }
            }

            var result = new List<PredictedTrajectory>();
            int colorIdx = 0;

            foreach (var hpa in levels)
            {
                var speedKey = hpa == 9999 ? "windspeed_10m"     : $"windspeed_{hpa}hPa";
                var dirKey   = hpa == 9999 ? "winddirection_10m" : $"winddirection_{hpa}hPa";

                var speeds = hourly.GetProperty(speedKey).EnumerateArray().ToList();
                var dirs   = hourly.GetProperty(dirKey).EnumerateArray().ToList();

                var points = new List<TrajectoryPoint> { new(lat, lon) };
                double curLat = lat, curLon = lon;
                const int stepMin = 5;

                for (int min = 0; min < durationMinutes; min += stepMin)
                {
                    int hourOffset = min / 60;
                    int dataIdx = Math.Min(idx + hourOffset, speeds.Count - 1);

                    double speedKmh = speeds[dataIdx].GetDouble();
                    double dirDeg   = dirs[dataIdx].GetDouble();

                    double moveDirRad = (dirDeg + 180) % 360 * Math.PI / 180;
                    double distKm = speedKmh * (stepMin / 60.0);

                    const double R = 6371.0;
                    double dLat = distKm / R * Math.Cos(moveDirRad) * (180 / Math.PI);
                    double dLon = distKm / R * Math.Sin(moveDirRad) / Math.Cos(curLat * Math.PI / 180) * (180 / Math.PI);

                    curLat += dLat;
                    curLon += dLon;
                    points.Add(new(curLat, curLon));
                }

                result.Add(new PredictedTrajectory(hpa, LevelAltitudes[hpa], points, Colors[colorIdx % Colors.Length]));
                colorIdx++;
            }

            return result;
        }
        catch { return new(); }
    }

    public static int ToFeet(int metres) => (int)Math.Round(metres * 3.28084);
    public static Dictionary<int, int> AvailableLevels => LevelAltitudes;

    public static List<TrajectoryResultEntry> ToResultEntries(List<PredictedTrajectory> trajectories, double startLat, double startLon)
    {
        return trajectories.Select(t =>
        {
            var end = t.Points.Last();
            double dist = 0;
            for (int i = 1; i < t.Points.Count; i++)
                dist += Haversine(t.Points[i-1].Lat, t.Points[i-1].Lon, t.Points[i].Lat, t.Points[i].Lon);
            // bearing from start to end
            double dLon = (end.Lon - startLon) * Math.PI / 180;
            double lat1 = startLat * Math.PI / 180, lat2 = end.Lat * Math.PI / 180;
            double bearing = Math.Atan2(Math.Sin(dLon) * Math.Cos(lat2),
                Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));
            double bearingDeg = (bearing * 180 / Math.PI + 360) % 360;
            return new TrajectoryResultEntry(t.HpaLevel, t.AltitudeM, end.Lat, end.Lon, dist, dist / (t.Points.Count * 5.0 / 60.0), bearingDeg);
        }).ToList();
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat/2) * Math.Sin(dLat/2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon/2) * Math.Sin(dLon/2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }
}
