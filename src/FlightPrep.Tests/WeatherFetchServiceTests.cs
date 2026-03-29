using System.Net;
using FlightPrep.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace FlightPrep.Tests;

public class WeatherFetchServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static ILogger<WeatherFetchService> NullLogger =>
        new Mock<ILogger<WeatherFetchService>>().Object;

    private static HttpClient CreateMockClient(
        string baseUrl,
        string responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody),
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri(baseUrl) };
    }

    private static HttpClient CreateThrowingClient(string baseUrl)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Simulated network error"));
        return new HttpClient(handler.Object) { BaseAddress = new Uri(baseUrl) };
    }

    /// <summary>Creates a WeatherFetchService wired to the given named client.</summary>
    private static WeatherFetchService BuildSvc(
        string clientName, HttpClient client) =>
        BuildSvc(clientName, client, NullLogger);

    private static WeatherFetchService BuildSvc(
        string clientName, HttpClient client, ILogger<WeatherFetchService> logger)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(clientName)).Returns(client);
        return new WeatherFetchService(factory.Object, logger);
    }

    // ── FetchMetarAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FetchMetarAsync_ValidIcaoAndMockResponse_ReturnsMetarString()
    {
        const string metar = "EBOS 241320Z 24010KT 9999 FEW025 15/08 Q1015";
        var svc = BuildSvc("aviationweather",
            CreateMockClient("https://aviationweather.gov/", metar));

        var result = await svc.FetchMetarAsync("EBOS");

        Assert.NotNull(result);
        Assert.StartsWith("EBOS", result);
    }

    [Fact]
    public async Task FetchMetarAsync_HttpThrows_ReturnsNull()
    {
        var svc = BuildSvc("aviationweather",
            CreateThrowingClient("https://aviationweather.gov/"));

        var result = await svc.FetchMetarAsync("EBOS");

        Assert.Null(result);
    }

    // ── FetchTafAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchTafAsync_HttpReturnsEmpty_ReturnsNullOrEmpty()
    {
        var svc = BuildSvc("aviationweather",
            CreateMockClient("https://aviationweather.gov/", ""));

        var result = await svc.FetchTafAsync("EBOS");

        Assert.True(result is null || result.Length == 0,
            $"Expected null or empty TAF result, got: '{result}'");
    }

    // ── FetchForecastAsync ────────────────────────────────────────────────────

    private const string ValidForecastJson = """
        {
          "hourly": {
            "time":                     ["2026-01-01T00:00", "2026-01-01T01:00", "2026-01-01T02:00"],
            "temperature_2m":           [5.5, 6.0, 4.8],
            "windspeed_10m":            [10.0, 12.5, 9.0],
            "winddirection_10m":        [180, 200, 190],
            "precipitation_probability":[20, 30, 10]
          }
        }
        """;

    [Fact]
    public async Task FetchForecastAsync_ValidJson_ReturnsCorrectHourlyList()
    {
        var svc = BuildSvc("openmeteo",
            CreateMockClient("https://api.open-meteo.com/", ValidForecastJson));

        var result = await svc.FetchForecastAsync(50.85, 4.35);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(5.5, result[0].TempC, 2);
        Assert.Equal(10.0, result[0].WindSpeedKmh, 2);
        Assert.Equal(180, result[0].WindDirDeg);
        Assert.Equal(20, result[0].PrecipProb);
    }

    [Fact]
    public async Task FetchForecastAsync_HttpThrows_ReturnsEmptyList()
    {
        var svc = BuildSvc("openmeteo",
            CreateThrowingClient("https://api.open-meteo.com/"));

        var result = await svc.FetchForecastAsync(50.85, 4.35);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── FetchWindProfileAsync ─────────────────────────────────────────────────

    /// <summary>
    /// Minimal valid open-meteo wind-profile response for
    /// flightDateTime = 2026-04-01T06:00 (index 0 in the time series).
    /// </summary>
    private const string ValidWindProfileJson = """
        {
          "hourly": {
            "time": ["2026-04-01T06:00", "2026-04-01T07:00"],
            "windspeed_1000hPa":  [18.52, 20.0],
            "winddirection_1000hPa": [270.0, 275.0],
            "windspeed_975hPa":   [20.0, 22.0],
            "winddirection_975hPa": [265.0, 268.0],
            "windspeed_950hPa":   [22.0, 24.0],
            "winddirection_950hPa": [260.0, 262.0],
            "windspeed_925hPa":   [24.0, 26.0],
            "winddirection_925hPa": [255.0, 258.0],
            "windspeed_900hPa":   [26.0, 28.0],
            "winddirection_900hPa": [250.0, 252.0],
            "windspeed_850hPa":   [28.0, 30.0],
            "winddirection_850hPa": [245.0, 248.0],
            "windspeed_800hPa":   [30.0, 32.0],
            "winddirection_800hPa": [240.0, 242.0],
            "windspeed_700hPa":   [32.0, 34.0],
            "winddirection_700hPa": [235.0, 238.0]
          }
        }
        """;

    private static readonly DateTime TargetHour = new(2026, 4, 1, 6, 0, 0);

    [Fact]
    public async Task FetchWindProfileAsync_FutureDate_ReturnsMappedWindLevels()
    {
        // Arrange
        var svc = BuildSvc("openmeteo",
            CreateMockClient("https://api.open-meteo.com/", ValidWindProfileJson));

        // Act
        var result = await svc.FetchWindProfileAsync(50.85, 4.35, TargetHour);

        // Assert – one entry per pressure level (8 total)
        Assert.Equal(8, result.Count);
        Assert.All(result, w => Assert.True(w.SpeedKt >= 0));
        Assert.All(result, w => Assert.InRange(w.DirectionDeg!.Value, 0, 360));
    }

    [Fact]
    public async Task FetchWindProfileAsync_TargetHourNotInTimeSeries_ReturnsEmpty()
    {
        // Arrange – JSON only has 06:00 and 07:00; request 09:00
        var svc = BuildSvc("openmeteo",
            CreateMockClient("https://api.open-meteo.com/", ValidWindProfileJson));
        var missingHour = new DateTime(2026, 4, 1, 9, 0, 0);

        // Act
        var result = await svc.FetchWindProfileAsync(50.85, 4.35, missingHour);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchWindProfileAsync_HttpThrows_ReturnsEmpty()
    {
        // Arrange
        var svc = BuildSvc("openmeteo",
            CreateThrowingClient("https://api.open-meteo.com/"));

        // Act
        var result = await svc.FetchWindProfileAsync(50.85, 4.35, TargetHour);

        // Assert – exception must not propagate; service returns empty list
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchWindProfileAsync_MalformedJson_ReturnsEmpty()
    {
        // Arrange
        var svc = BuildSvc("openmeteo",
            CreateMockClient("https://api.open-meteo.com/", "not valid json"));

        // Act
        var result = await svc.FetchWindProfileAsync(50.85, 4.35, TargetHour);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchWindProfileAsync_SpeedConversion_CorrectKtValue()
    {
        // Arrange – 18.52 km/h ÷ 1.852 = 10.0 kt → rounds to 10
        var svc = BuildSvc("openmeteo",
            CreateMockClient("https://api.open-meteo.com/", ValidWindProfileJson));

        // Act
        var result = await svc.FetchWindProfileAsync(50.85, 4.35, TargetHour);

        // Assert – first level (1000 hPa) has windspeed_1000hPa = 18.52 km/h → 10 kt
        Assert.Equal(10, result[0].SpeedKt);
    }
}
