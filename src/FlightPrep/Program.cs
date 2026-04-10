using FlightPrep.Components;
using FlightPrep.Domain.Services;
using FlightPrep.Endpoints;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Infrastructure.Services;
using FlightPrep.Services;
using FlightPrep.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using QuestPDF;
using QuestPDF.Infrastructure;
using Serilog;
using System.Net;
using IPNetwork = System.Net.IPNetwork;

Settings.License = LicenseType.Community;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console();

    var connStr = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrEmpty(connStr))
    {
        var telemetryClient = new TelemetryClient(
            new TelemetryConfiguration { ConnectionString = connStr });
        cfg.WriteTo.ApplicationInsights(telemetryClient, TelemetryConverter.Traces);
    }
});

builder.Services.AddDbContextFactory<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity requires a scoped DbContext alongside the factory
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

builder.Services.AddSingleton<ISunriseService, SunriseService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IGoNoGoService, GoNoGoService>();
builder.Services.AddScoped<IOFPSettingsService, OFPSettingsService>();
builder.Services.AddScoped<IFlightAssessmentService, FlightAssessmentService>();
builder.Services.AddScoped<IFlightPreparationService, FlightPreparationService>();
builder.Services.AddScoped<IPilotService, PilotService>();
builder.Services.AddScoped<IBalloonService, BalloonService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// Application Insights — only active when APPLICATIONINSIGHTS_CONNECTION_STRING is set
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FlightTelemetryInitializer>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddProcessor(sp => sp.GetRequiredService<FlightTelemetryInitializer>()));
builder.Services.AddSingleton<IKmlService, KmlService>();
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
builder.Services.AddHttpClient<ITrajectoryMapService, TrajectoryMapService>(c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("FlightPrep/1.0 (+https://github.com/NickThys3012/FlightPrep)");
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IWeatherFetchService, WeatherFetchService>();
builder.Services.AddScoped<IPowerLineService, PowerLineService>();
builder.Services.AddSingleton<IReleaseNotesService, ReleaseNotesService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

var app = builder.Build();

// Apply migrations (no-op in prod where CI/CD already migrated; required for docker-compose and local dev)
using (var scope = app.Services.CreateScope())
{
    var dbCtx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbCtx.Database.MigrateAsync();   // no-op in prod, required for docker-compose

    await AdminSeeder.SeedAdminAsync(scope.ServiceProvider);
}

// Must be first — rewrites Request.Scheme before UseHsts/UseHttpsRedirection read it
var forwardedOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
forwardedOptions.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
forwardedOptions.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
forwardedOptions.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
app.UseForwardedHeaders(forwardedOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
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
app.MapGet("/api/powerlines", async (double south, double west, double north, double east, IPowerLineService powerLineService) =>
{
    if (south >= north || west >= east ||
        south < -90 || north > 90 || west < -180 || east > 180)
    {
        return Results.BadRequest("Ongeldige bbox");
    }

    var geoJson = await powerLineService.GetGeoJsonAsync(south, west, north, east);
    return geoJson is null
        ? Results.StatusCode(502)
        : Results.Content(geoJson, "application/json");
});

// Tile proxy — serves OSM tiles same-origin so html2canvas can capture maps for PDF
TileProxyEndpoint.Map(app);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
