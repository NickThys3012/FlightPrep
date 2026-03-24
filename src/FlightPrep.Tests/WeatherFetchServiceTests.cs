using System.Net;
using FlightPrep.Services;
using Moq;
using Moq.Protected;

namespace FlightPrep.Tests;

public class WeatherFetchServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

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

    // ── FetchMetarAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FetchMetarAsync_ValidIcaoAndMockResponse_ReturnsMetarString()
    {
        const string metar = "EBOS 241320Z 24010KT 9999 FEW025 15/08 Q1015";
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("aviationweather"))
               .Returns(CreateMockClient("https://aviationweather.gov/", metar));
        var svc = new WeatherFetchService(factory.Object);

        var result = await svc.FetchMetarAsync("EBOS");

        Assert.NotNull(result);
        Assert.StartsWith("EBOS", result);
    }

    [Fact]
    public async Task FetchMetarAsync_HttpThrows_ReturnsNull()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("aviationweather"))
               .Returns(CreateThrowingClient("https://aviationweather.gov/"));
        var svc = new WeatherFetchService(factory.Object);

        var result = await svc.FetchMetarAsync("EBOS");

        Assert.Null(result);
    }

    // ── FetchTafAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchTafAsync_HttpReturnsEmpty_ReturnsNullOrEmpty()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("aviationweather"))
               .Returns(CreateMockClient("https://aviationweather.gov/", ""));
        var svc = new WeatherFetchService(factory.Object);

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
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("openmeteo"))
               .Returns(CreateMockClient("https://api.open-meteo.com/", ValidForecastJson));
        var svc = new WeatherFetchService(factory.Object);

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
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("openmeteo"))
               .Returns(CreateThrowingClient("https://api.open-meteo.com/"));
        var svc = new WeatherFetchService(factory.Object);

        var result = await svc.FetchForecastAsync(50.85, 4.35);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
