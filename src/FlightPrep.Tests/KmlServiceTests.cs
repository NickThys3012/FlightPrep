using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Services;

namespace FlightPrep.Tests;

public class KmlServiceTests
{
    private readonly KmlService _sut = new();

    private static string BuildKml(string coordsText) => $"""
                                                          <?xml version="1.0" encoding="UTF-8"?>
                                                          <kml xmlns="http://www.opengis.net/kml/2.2">
                                                            <Document>
                                                              <Placemark>
                                                                <LineString>
                                                                  <coordinates>{coordsText}</coordinates>
                                                                </LineString>
                                                              </Placemark>
                                                            </Document>
                                                          </kml>
                                                          """;

    [Fact]
    public void ParseCoordinates_ValidKml_ReturnsCorrectCountAndValues()
    {
        var kml = BuildKml("4.35,50.85,100 4.36,50.86,200");

        var result = _sut.ParseCoordinates(kml);

        Assert.Equal(2, result.Count);
        Assert.Equal(50.85, result[0].Lat, 5);
        Assert.Equal(4.35, result[0].Lon, 5);
        Assert.Equal(100, result[0].AltM, 1);
        Assert.Equal(50.86, result[1].Lat, 5);
        Assert.Equal(4.36, result[1].Lon, 5);
        Assert.Equal(200, result[1].AltM, 1);
    }

    [Fact]
    public void ParseCoordinates_EmptyString_ReturnsEmptyList()
    {
        var result = _sut.ParseCoordinates(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseCoordinates_MalformedXml_ReturnsEmptyList()
    {
        var result = _sut.ParseCoordinates("<not valid xml<<<");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseCoordinates_KmlMissingNamespace_ReturnsEmptyList()
    {
        // No xmlns="http://www.opengis.net/kml/2.2" — service won't find the element
        var kml = """
                  <?xml version="1.0"?>
                  <kml>
                    <Document>
                      <Placemark>
                        <LineString>
                          <coordinates>4.35,50.85,100</coordinates>
                        </LineString>
                      </Placemark>
                    </Document>
                  </kml>
                  """;

        var result = _sut.ParseCoordinates(kml);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeStats_EmptyList_ReturnsAllZeros()
    {
        var (max, min, dist) = _sut.ComputeStats([]);

        Assert.Equal(0, max);
        Assert.Equal(0, min);
        Assert.Equal(0, dist);
    }

    [Fact]
    public void ComputeStats_TwoKnownPoints_ReturnsCorrectAltitudesAndDistance()
    {
        var pts = new List<TrackPoint> { new(50.85, 4.35, 100), new(51.20, 4.42, 300) };

        var (max, min, dist) = _sut.ComputeStats(pts);

        Assert.Equal(300, max, 1);
        Assert.Equal(100, min, 1);
        // Haversine Brussels-area: ~39 km — allow ±5 %
        Assert.InRange(dist, 37.0, 42.0);
    }
}
