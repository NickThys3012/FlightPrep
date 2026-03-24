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
// Data protection keys persist to /root/.aspnet/DataProtection-Keys (mounted as Docker volume)

// Application Insights — only active when APPLICATIONINSIGHTS_CONNECTION_STRING is set
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<KmlService>();

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
builder.Services.AddScoped<WeatherFetchService>();
builder.Services.AddScoped<TrajectoryService>();

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
