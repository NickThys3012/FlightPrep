using System.Globalization;
using System.Text.Json;

namespace FlightPrep.Services;

public record WindAtLevel(int HpaLevel, int AltitudeM, double SpeedKmh, double DirectionDeg);
public record TrajectoryPoint(double Lat, double Lon);
public record PredictedTrajectory(int HpaLevel, int AltitudeM, List<TrajectoryPoint> Points, string Color);

public class TrajectoryService(IHttpClientFactory httpFactory)
{
    private static readonly Dictionary<int, int> LevelAltitudes = new()
    {
        { 850, 1500 }, { 800, 1950 }, { 750, 2450 }, { 700, 3000 },
        { 650, 3600 }, { 600, 4200 }, { 500, 5500 }
    };

    private static readonly string[] Colors =
        { "#e74c3c", "#3498db", "#2ecc71", "#f39c12", "#9b59b6", "#1abc9c", "#e67e22" };

    public async Task<List<PredictedTrajectory>> PredictAsync(
        double lat, double lon,
        DateTime flightTimeLocal,
        int[] hpaLevels,
        int durationMinutes)
    {
        var levels = hpaLevels.Where(l => LevelAltitudes.ContainsKey(l)).ToArray();
        if (levels.Length == 0) return new();

        var hourlyParams = string.Join(",", levels.SelectMany(l =>
            new[] { $"windspeed_{l}hPa", $"winddirection_{l}hPa" }));

        var url = $"v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&hourly={hourlyParams}&forecast_days=3&timezone=Europe%2FBrussels";

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
                var speeds = hourly.GetProperty($"windspeed_{hpa}hPa").EnumerateArray().ToList();
                var dirs   = hourly.GetProperty($"winddirection_{hpa}hPa").EnumerateArray().ToList();

                var points = new List<TrajectoryPoint> { new(lat, lon) };
                double curLat = lat, curLon = lon;
                int stepMin = 5;

                for (int min = 0; min < durationMinutes; min += stepMin)
                {
                    int hourOffset = min / 60;
                    int dataIdx = Math.Min(idx + hourOffset, speeds.Count - 1);

                    double speedKmh = speeds[dataIdx].GetDouble();
                    double dirDeg   = dirs[dataIdx].GetDouble();

                    // Wind direction is where wind comes FROM; balloon moves toward opposite direction
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

    public static Dictionary<int, int> AvailableLevels => LevelAltitudes;
}
