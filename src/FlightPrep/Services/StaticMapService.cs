using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlightPrep.Services;

/// <summary>One polyline to draw on the static map.</summary>
public record TrajectoryPolyline(string HexColor, IReadOnlyList<(double Lat, double Lon)> Points);

/// <summary>
/// Generates a static PNG map by fetching OSM tiles server-side and drawing
/// trajectory polylines on top. Used by PdfService to embed a visual map in the PDF.
/// </summary>
public class StaticMapService(IHttpClientFactory httpFactory)
{
    private const int TileSize = 256;
    private const string TileUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// Returns PNG bytes (600×380) or null when there are no points.
    /// Silently continues with a white tile background when any tile fetch fails.
    /// </summary>
    public async Task<byte[]?> GenerateAsync(
        double startLat,
        double startLon,
        IEnumerable<TrajectoryPolyline> polylines,
        int widthPx = 600,
        int heightPx = 380,
        CancellationToken ct = default)
    {
        var lines = polylines.Where(l => l.Points.Count >= 2).ToList();

        // Collect all coordinates (include launch point + all trajectory points)
        var allLats = new List<double> { startLat };
        var allLons = new List<double> { startLon };
        foreach (var line in lines)
            foreach (var pt in line.Points)
            {
                allLats.Add(pt.Lat);
                allLons.Add(pt.Lon);
            }

        if (allLats.Count < 2 && lines.Count == 0) return null;

        // Bounding box with ~15% padding (or minimum 0.04°)
        double pad = Math.Max((allLats.Max() - allLats.Min()) * 0.15, 0.04);
        double minLat = allLats.Min() - pad;
        double maxLat = allLats.Max() + pad;
        double minLon = allLons.Min() - pad;
        double maxLon = allLons.Max() + pad;

        int zoom = ChooseZoom(minLat, maxLat, minLon, maxLon, widthPx, heightPx);

        // Tile range: south-west has larger tile-Y, north-east has smaller tile-Y
        (int swTX, int swTY) = LatLonToTile(minLat, minLon, zoom); // south-west = large Y
        (int neTX, int neTY) = LatLonToTile(maxLat, maxLon, zoom); // north-east = small Y

        int minTX = Math.Min(swTX, neTX);
        int maxTX = Math.Max(swTX, neTX);
        int minTY = Math.Min(swTY, neTY);  // north has smaller Y
        int maxTY = Math.Max(swTY, neTY);  // south has larger Y

        int tileCountX = maxTX - minTX + 1;
        int tileCountY = maxTY - minTY + 1;

        // Fetch all tiles in parallel
        var http = httpFactory.CreateClient("staticmap");
        var tileTasks = new Dictionary<(int ix, int iy), Task<byte[]?>>();
        for (int tx = minTX; tx <= maxTX; tx++)
            for (int ty = minTY; ty <= maxTY; ty++)
                tileTasks[(tx - minTX, ty - minTY)] = FetchTileAsync(http, zoom, tx, ty, ct);

        await Task.WhenAll(tileTasks.Values);

        // Stitch tiles into one image
        int imgW = tileCountX * TileSize;
        int imgH = tileCountY * TileSize;
        using var image = new Image<Rgba32>(imgW, imgH, new Rgba32(240, 240, 240)); // light grey fallback

        foreach (var ((ix, iy), task) in tileTasks)
        {
            var bytes = task.Result;
            if (bytes is null) continue;
            try
            {
                using var tile = Image.Load<Rgba32>(bytes);
                var destPoint = new Point(ix * TileSize, iy * TileSize);
                image.Mutate(ctx => ctx.DrawImage(tile, destPoint, 1f));
            }
            catch { /* corrupted tile — skip */ }
        }

        // Draw trajectory polylines + landing markers
        image.Mutate(ctx =>
        {
            foreach (var line in lines)
            {
                var color = ParseHexColor(line.HexColor);
                var pen   = Pens.Solid(color, 3f);

                for (int i = 0; i + 1 < line.Points.Count; i++)
                {
                    var (x1, y1) = LatLonToPixel(line.Points[i].Lat,     line.Points[i].Lon,     zoom, minTX, minTY);
                    var (x2, y2) = LatLonToPixel(line.Points[i + 1].Lat, line.Points[i + 1].Lon, zoom, minTX, minTY);
                    ctx.DrawLine(pen, new PointF((float)x1, (float)y1), new PointF((float)x2, (float)y2));
                }

                // Landing: filled circle with white border
                var last     = line.Points[^1];
                var (lx, ly) = LatLonToPixel(last.Lat, last.Lon, zoom, minTX, minTY);
                ctx.Fill(Brushes.Solid(color),              new EllipsePolygon((float)lx, (float)ly, 6));
                ctx.Draw(Pens.Solid(Color.White, 1.5f),     new EllipsePolygon((float)lx, (float)ly, 6));
            }

            // Launch marker: white circle with dark border
            var (sx, sy) = LatLonToPixel(startLat, startLon, zoom, minTX, minTY);
            ctx.Fill(Brushes.Solid(Color.White),                                  new EllipsePolygon((float)sx, (float)sy, 8));
            ctx.Draw(Pens.Solid(Color.ParseHex("1a3a5c"), 2.5f), new EllipsePolygon((float)sx, (float)sy, 8));
        });

        // Resize to target dimensions
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(widthPx, heightPx),
            Mode = ResizeMode.Stretch
        }));

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms, ct);
        return ms.ToArray();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static async Task<byte[]?> FetchTileAsync(HttpClient http, int zoom, int tx, int ty, CancellationToken ct)
    {
        try
        {
            var url = TileUrl
                .Replace("{z}", zoom.ToString())
                .Replace("{x}", tx.ToString())
                .Replace("{y}", ty.ToString());
            return await http.GetByteArrayAsync(url, ct);
        }
        catch { return null; }
    }

    /// <summary>OSM/Web-Mercator tile coordinate for a lat/lon at a given zoom.</summary>
    private static (int x, int y) LatLonToTile(double lat, double lon, int zoom)
    {
        var n      = Math.Pow(2, zoom);
        var x      = (int)Math.Floor((lon + 180.0) / 360.0 * n);
        var latRad = lat * Math.PI / 180.0;
        var y      = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        int max    = (int)n - 1;
        return (Math.Clamp(x, 0, max), Math.Clamp(y, 0, max));
    }

    /// <summary>Pixel position on the stitched image for a lat/lon.</summary>
    private static (double px, double py) LatLonToPixel(double lat, double lon, int zoom, int minTileX, int minTileY)
    {
        var n      = Math.Pow(2, zoom);
        var xTile  = (lon + 180.0) / 360.0 * n;
        var latRad = lat * Math.PI / 180.0;
        var yTile  = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return ((xTile - minTileX) * TileSize, (yTile - minTileY) * TileSize);
    }

    /// <summary>
    /// Picks the highest zoom level where the full bounding box fits within
    /// at most 5×4 tiles (so images stay reasonable for PDF embedding).
    /// </summary>
    private static int ChooseZoom(double minLat, double maxLat, double minLon, double maxLon, int maxW, int maxH)
    {
        for (int z = 13; z >= 7; z--)
        {
            var (x0, y0) = LatLonToTile(minLat, minLon, z); // south-west
            var (x1, y1) = LatLonToTile(maxLat, maxLon, z); // north-east
            int spanX = Math.Abs(x1 - x0) + 1;
            int spanY = Math.Abs(y0 - y1) + 1;              // y0 (south) > y1 (north)
            if (spanX <= 5 && spanY <= 4 &&
                spanX * TileSize <= maxW * 2 &&
                spanY * TileSize <= maxH * 2)
                return z;
        }
        return 7;
    }

    private static Color ParseHexColor(string hex)
    {
        try { return Color.ParseHex(hex.TrimStart('#')); }
        catch  { return Color.Blue; }
    }
}
