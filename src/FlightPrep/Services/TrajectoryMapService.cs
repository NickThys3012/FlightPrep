using System.Text.Json;
using FlightPrep.Models.Trajectory;
using SkiaSharp;

namespace FlightPrep.Services;

/// <summary>
/// Generates a static JPEG map of trajectory simulations by fetching OSM tiles
/// server-side and compositing coloured polylines on top with shadow, legend,
/// and start/landing markers. Ported from BalloonPrep reference implementation.
/// </summary>
public class TrajectoryMapService(HttpClient http, ILogger<TrajectoryMapService> logger)
{
    private const int TileSize = 256;
    private const int LegendPad = 6;

    public async Task<byte[]?> RenderAsync(string? trajectorySimulationJson)
    {
        if (string.IsNullOrWhiteSpace(trajectorySimulationJson))
            return null;

        try
        {
            var trajs = JsonSerializer.Deserialize<List<SimulatedTrajectory>>(
                trajectorySimulationJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (trajs is null || trajs.Count == 0) return null;

            // Build series from trajectories that have at least 2 points
            var series = trajs
                .Where(t => t.Points.Count >= 2)
                .Select(t => (
                    Label:  $"{t.AltitudeFt} ft",
                    Colour: ParseSkColor(t.Color),
                    Pts:    t.Points.Select(p => (p.Lat, p.Lon)).ToList()
                ))
                .ToList();

            if (series.Count == 0) return null;

            // Bounding box
            var allLats = series.SelectMany(s => s.Pts.Select(p => p.Lat)).ToList();
            var allLons = series.SelectMany(s => s.Pts.Select(p => p.Lon)).ToList();
            double minLat = allLats.Min(), maxLat = allLats.Max();
            double minLon = allLons.Min(), maxLon = allLons.Max();
            var latPad = Math.Max(maxLat - minLat, 0.03) * 0.25;
            var lonPad = Math.Max(maxLon - minLon, 0.03) * 0.25;
            minLat -= latPad; maxLat += latPad;
            minLon -= lonPad; maxLon += lonPad;

            var zoom = Math.Clamp(ChooseZoom(minLon, maxLon, 5), 7, 12);

            var (txMin, tyMin) = LatLonToTile(maxLat, minLon, zoom); // NW corner
            var (txMax, tyMax) = LatLonToTile(minLat, maxLon, zoom); // SE corner
            int txCount = Math.Min(txMax - txMin + 1, 8);
            int tyCount = Math.Min(tyMax - tyMin + 1, 8);
            int imgW = txCount * TileSize;
            int imgH = tyCount * TileSize;

            // Fetch tiles in parallel (max 6 concurrent)
            var tileImages = new SKBitmap?[txCount, tyCount];
            int fetchedCount = 0;
            await Parallel.ForEachAsync(
                Enumerable.Range(0, txCount * tyCount),
                new ParallelOptions { MaxDegreeOfParallelism = 6 },
                async (idx, _) =>
                {
                    int col = idx % txCount, row = idx / txCount;
                    var bmp = await FetchTileAsync(zoom, txMin + col, tyMin + row);
                    tileImages[col, row] = bmp;
                    if (bmp != null) Interlocked.Increment(ref fetchedCount);
                });

            logger.LogInformation("Trajectory map: {Ok}/{Total} tiles fetched at zoom {Z}",
                fetchedCount, txCount * tyCount, zoom);

            // Stitch tiles
            var info = new SKImageInfo(imgW, imgH);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(200, 220, 240)); // fallback ocean blue

            for (int col = 0; col < txCount; col++)
                for (int row = 0; row < tyCount; row++)
                    if (tileImages[col, row] is { } bmp)
                    {
                        canvas.DrawBitmap(bmp, col * TileSize, row * TileSize);
                        bmp.Dispose();
                    }

            // Coordinate → pixel helpers (capture zoom/offset in closure)
            float ToX(double lon) => (float)((LonToFrac(lon, zoom) - txMin) * TileSize);
            float ToY(double lat) => (float)((LatToFrac(lat, zoom) - tyMin) * TileSize);

            // Draw each trajectory
            foreach (var (_, colour, pts) in series)
                DrawTrajectory(canvas, pts, colour, ToX, ToY);

            DrawLegend(canvas, series, imgW, imgH);

            // Resize if needed, export as JPEG
            using var snap = surface.Snapshot();
            const int MaxW = 1200, MaxH = 900;
            byte[] result;
            if (imgW > MaxW || imgH > MaxH)
            {
                float scale = Math.Min((float)MaxW / imgW, (float)MaxH / imgH);
                int sw = Math.Max(1, (int)(imgW * scale));
                int sh = Math.Max(1, (int)(imgH * scale));
                using var bmp    = SKBitmap.FromImage(snap);
                using var scaled = bmp.Resize(new SKImageInfo(sw, sh), SKFilterQuality.High);
                using var sImg   = SKImage.FromBitmap(scaled);
                using var data   = sImg.Encode(SKEncodedImageFormat.Jpeg, 88);
                result = data.ToArray();
            }
            else
            {
                using var data = snap.Encode(SKEncodedImageFormat.Jpeg, 88);
                result = data.ToArray();
            }

            logger.LogInformation("Trajectory map rendered: {Bytes} bytes", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TrajectoryMapService.RenderAsync failed");
            return null;
        }
    }

    // ── tile fetching ────────────────────────────────────────────────────────────

    private async Task<SKBitmap?> FetchTileAsync(int zoom, int x, int y)
    {
        int n = 1 << zoom;
        x = (x % n + n) % n; // wrap around anti-meridian
        var url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            return SKBitmap.Decode(bytes);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Tile fetch failed: {Url}", url);
            return null;
        }
    }

    // ── drawing ──────────────────────────────────────────────────────────────────

    private static void DrawTrajectory(
        SKCanvas canvas,
        List<(double Lat, double Lon)> pts,
        SKColor colour,
        Func<double, float> toX,
        Func<double, float> toY)
    {
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 110),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        using var linePaint = new SKPaint
        {
            Color = colour,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        using var path = new SKPath();
        path.MoveTo(toX(pts[0].Lon), toY(pts[0].Lat));
        for (int i = 1; i < pts.Count; i++)
            path.LineTo(toX(pts[i].Lon), toY(pts[i].Lat));

        canvas.DrawPath(path, shadowPaint);
        canvas.DrawPath(path, linePaint);

        // Intermediate dots every 3 points
        using var dotFill = new SKPaint { Color = colour, IsAntialias = true };
        using var dotRing = new SKPaint
            { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        for (int i = 3; i < pts.Count - 1; i += 3)
        {
            float x = toX(pts[i].Lon), y = toY(pts[i].Lat);
            canvas.DrawCircle(x, y, 4f, dotFill);
            canvas.DrawCircle(x, y, 4f, dotRing);
        }

        // Start marker (green)
        float sx = toX(pts[0].Lon), sy = toY(pts[0].Lat);
        using var startFill = new SKPaint { Color = new SKColor(25, 135, 84), IsAntialias = true };
        using var startRing = new SKPaint
            { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
        canvas.DrawCircle(sx, sy, 7f, startFill);
        canvas.DrawCircle(sx, sy, 7f, startRing);

        // Landing marker (red)
        float ex = toX(pts[^1].Lon), ey = toY(pts[^1].Lat);
        using var endFill = new SKPaint { Color = new SKColor(220, 53, 69), IsAntialias = true };
        using var endRing = new SKPaint
            { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
        canvas.DrawCircle(ex, ey, 7f, endFill);
        canvas.DrawCircle(ex, ey, 7f, endRing);
    }

    private static void DrawLegend(
        SKCanvas canvas,
        List<(string Label, SKColor Colour, List<(double Lat, double Lon)> Pts)> series,
        int imgW, int imgH)
    {
        const float rowH = 18f, boxW = 100f;
        float boxH = series.Count * rowH + 10f;
        float lx = imgW - boxW - LegendPad;
        float ly = LegendPad;

        using var bg     = new SKPaint { Color = new SKColor(255, 255, 255, 210) };
        using var border = new SKPaint
            { Color = new SKColor(100, 100, 100, 160), Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f };
        canvas.DrawRoundRect(lx, ly, boxW, boxH, 5, 5, bg);
        canvas.DrawRoundRect(lx, ly, boxW, boxH, 5, 5, border);

        for (int i = 0; i < series.Count; i++)
        {
            var (label, colour, _) = series[i];
            float y = ly + 8f + i * rowH + rowH / 2f;
            using var lp = new SKPaint
                { Color = colour, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };
            canvas.DrawLine(lx + 6f, y, lx + 24f, y, lp);
            using var dp = new SKPaint { Color = colour, IsAntialias = true };
            canvas.DrawCircle(lx + 15f, y, 4f, dp);
            using var tp = new SKPaint { Color = SKColors.Black, TextSize = 11f, IsAntialias = true };
            canvas.DrawText(label, lx + 28f, y + 4f, tp);
        }
    }

    // ── tile math ────────────────────────────────────────────────────────────────

    private static (int X, int Y) LatLonToTile(double lat, double lon, int zoom)
        => ((int)LonToFrac(lon, zoom), (int)LatToFrac(lat, zoom));

    private static double LonToFrac(double lon, int zoom)
        => (lon + 180.0) / 360.0 * (1 << zoom);

    private static double LatToFrac(double lat, int zoom)
    {
        var r = lat * Math.PI / 180.0;
        return (1.0 - Math.Log(Math.Tan(r) + 1.0 / Math.Cos(r)) / Math.PI) / 2.0 * (1 << zoom);
    }

    private static int ChooseZoom(double minLon, double maxLon, int targetTiles)
    {
        var lonRange = maxLon - minLon;
        if (lonRange <= 0) return 10;
        return (int)Math.Round(Math.Log2(targetTiles * 360.0 / lonRange));
    }

    private static SKColor ParseSkColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            uint v = Convert.ToUInt32(hex, 16);
            return hex.Length == 6
                ? new SKColor((byte)(v >> 16), (byte)(v >> 8), (byte)v)
                : new SKColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }
        catch { return SKColors.Blue; }
    }
}
