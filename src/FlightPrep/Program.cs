using FlightPrep.Components;
using FlightPrep.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF;
using QuestPDF.Infrastructure;
using Serilog;

Settings.License = LicenseType.Community;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddDbContextFactory<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<WeatherService>();
builder.Services.AddSingleton<SunriseService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<GoNoGoService>();
builder.Services.AddScoped<FlightPreparationService>();
// Data protection keys persist to /root/.aspnet/DataProtection-Keys (mounted as Docker volume)

// Application Insights — only active when APPLICATIONINSIGHTS_CONNECTION_STRING is set
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<KmlService>();
builder.Services.AddSingleton<ITrajectoryService, TrajectoryService>();
builder.Services.AddScoped<IEnhancedTrajectoryService, EnhancedTrajectoryService>();

builder.Services.AddHttpClient("aviationweather", c =>
{
    c.BaseAddress = new Uri("https://aviationweather.gov/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("FlightPrep/1.0");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("openmeteo", c =>
{
    c.BaseAddress = new Uri("https://api.open-meteo.com/");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("overpass", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("staticmap", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("FlightPrep/1.0 (+https://github.com/NickThys3012/FlightPrep)");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<TrajectoryMapService>(c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("FlightPrep/1.0 (+https://github.com/NickThys3012/FlightPrep)");
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WeatherFetchService>();
builder.Services.AddScoped<PowerLineService>();
builder.Services.AddSingleton<ReleaseNotesService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Auto-apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    var db = dbFactory.CreateDbContext();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name == "service-worker.js")
        {
            ctx.Context.Response.Headers["Service-Worker-Allowed"] = "/";
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
        }
    }
});
app.MapStaticAssets();
app.MapGet("/api/powerlines", async (double south, double west, double north, double east, PowerLineService powerLineService) =>
{
    if (south >= north || west >= east ||
        south < -90 || north > 90 || west < -180 || east > 180)
        return Results.BadRequest("Ongeldige bbox");

    var geoJson = await powerLineService.GetGeoJsonAsync(south, west, north, east);
    return geoJson is null
        ? Results.StatusCode(502)
        : Results.Content(geoJson, "application/json");
});

// Tile proxy — serves OSM tiles same-origin so html2canvas can capture maps for PDF
app.MapGet("/tiles/{z}/{x}/{y}", async (int z, int x, int y, IHttpClientFactory httpFactory, HttpContext ctx) =>
{
    if (z is < 0 or > 19 || x < 0 || y < 0) return Results.BadRequest();
    var client = httpFactory.CreateClient("staticmap");
    try
    {
        var bytes = await client.GetByteArrayAsync(
            $"https://tile.openstreetmap.org/{z}/{x}/{y}.png");
        ctx.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.Bytes(bytes, "image/png");
    }
    catch { return Results.StatusCode(502); }
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
