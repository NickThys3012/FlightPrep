using FlightPrep.Infrastructure.Services;
using System.Net;

namespace FlightPrep.Tests;

public class WeatherServiceTests
{
    private static WeatherService BuildWithHandler(MockWeatherHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://aviationweather.gov/") };
        return new WeatherService(client);
    }

    [Fact]
    public async Task FetchAsync_ValidIcao_ReturnsTrimmedMetarAndTaf()
    {
        var handler = new MockWeatherHandler(
            "  EBBR 261020Z 22010KT  ",
            "  TAF EBBR 261000Z  ");
        var sut = BuildWithHandler(handler);

        var (metar, taf) = await sut.FetchAsync("EBBR");

        Assert.Equal("EBBR 261020Z 22010KT", metar);
        Assert.Equal("TAF EBBR 261000Z", taf);
    }

    [Fact]
    public async Task FetchAsync_EmptyMetarResponse_ReturnsNullMetar()
    {
        var handler = new MockWeatherHandler("   ", "TAF EBBR");
        var sut = BuildWithHandler(handler);

        var (metar, _) = await sut.FetchAsync("EBBR");

        Assert.Null(metar);
    }

    [Fact]
    public async Task FetchAsync_EmptyTafResponse_ReturnsNullTaf()
    {
        var handler = new MockWeatherHandler("EBBR 261020Z", "");
        var sut = BuildWithHandler(handler);

        var (_, taf) = await sut.FetchAsync("EBBR");

        Assert.Null(taf);
    }

    [Fact]
    public async Task FetchAsync_HttpException_ReturnsBothNull()
    {
        var handler = new MockWeatherHandler(throwException: true);
        var sut = BuildWithHandler(handler);

        var (metar, taf) = await sut.FetchAsync("EBBR");

        Assert.Null(metar);
        Assert.Null(taf);
    }

    [Fact]
    public async Task FetchAsync_ServerError_ReturnsBothNull()
    {
        var handler = new MockWeatherHandler(statusCode: HttpStatusCode.InternalServerError);
        var sut = BuildWithHandler(handler);

        var (metar, taf) = await sut.FetchAsync("EBBR");

        Assert.Null(metar);
        Assert.Null(taf);
    }
}

internal class MockWeatherHandler(
    string? metar = null,
    string? taf = null,
    bool throwException = false,
    HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (throwException)
        {
            throw new HttpRequestException("Simulated network error");
        }

        if (statusCode != HttpStatusCode.OK)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }

        var body = request.RequestUri?.PathAndQuery.Contains("metar") == true
            ? metar ?? ""
            : taf ?? "";

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
