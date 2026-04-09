namespace FlightPrep.Endpoints;

/// <summary>
///     Serves OpenStreetMap tiles from the same origin so html2canvas can capture
///     maps for PDF export without running into cross-origin restrictions.
/// </summary>
public static class TileProxyEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/tiles/{z}/{x}/{y}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        int z, int x, int y,
        IHttpClientFactory httpFactory,
        HttpContext ctx)
    {
        if (z is < 0 or > 19 || x < 0 || y < 0)
        {
            return Results.BadRequest();
        }

        var client = httpFactory.CreateClient("staticmap");
        try
        {
            var bytes = await client.GetByteArrayAsync(
                $"https://tile.openstreetmap.org/{z}/{x}/{y}.png");
            ctx.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.Bytes(bytes, "image/png");
        }
        catch
        {
            return Results.StatusCode(502);
        }
    }
}
