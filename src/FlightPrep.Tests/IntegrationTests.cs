using FlightPrep.Infrastructure.Services;
using FlightPrep.Services;

namespace FlightPrep.Tests;

/// <summary>
///     Integration-style tests that exercise service behaviour end-to-end
///     using only in-process logic (no network, no DB).
/// </summary>
public class IntegrationTests
{
    // ── KmlService: round-trip parse → ComputeStats ───────────────────────────

    private readonly KmlService _kmlService = new();
    // ── SunriseService: multi-city sunset > sunrise ───────────────────────────

    private readonly SunriseService _sunriseService = new();

    [Theory]
    [InlineData("Paris", 48.85, 2.35)]
    [InlineData("London", 51.51, -0.13)]
    [InlineData("Reykjavik", 64.13, -21.93)]
    public void SunriseService_NormalAutumnDate_SunsetIsAfterSunrise(
        string city, double lat, double lon)
    {
        // Sep 15 is well within the normal day/night cycle for all three cities
        var (sunrise, sunset) = _sunriseService.Calculate(new DateOnly(2026, 9, 15), lat, lon);

        Assert.True(sunset > sunrise,
            $"{city}: expected sunset ({sunset}) to be after sunrise ({sunrise})");
    }

    [Fact]
    public void KmlService_RoundTrip_ThreeKnownPoints_CorrectDistanceWithinOnePct()
    {
        // A = (51.0°N, 4.0°E, 100 m)
        // B = (51.5°N, 4.5°E, 200 m)
        // C = (52.0°N, 5.0°E, 150 m)
        // Haversine A→B ≈ 65.6 km, B→C ≈ 65.4 km → total ≈ 131.0 km
        const string kml = """
                           <?xml version="1.0" encoding="UTF-8"?>
                           <kml xmlns="http://www.opengis.net/kml/2.2">
                             <Document>
                               <Placemark>
                                 <LineString>
                                   <coordinates>4.0,51.0,100 4.5,51.5,200 5.0,52.0,150</coordinates>
                                 </LineString>
                               </Placemark>
                             </Document>
                           </kml>
                           """;

        var pts = _kmlService.ParseCoordinates(kml);
        Assert.Equal(3, pts.Count);

        var (maxAlt, minAlt, distKm) = _kmlService.ComputeStats(pts);

        Assert.Equal(200, maxAlt, 1);
        Assert.Equal(100, minAlt, 1);

        const double expected = 131.0;
        Assert.InRange(distKm, expected * 0.99, expected * 1.01);
    }
}
