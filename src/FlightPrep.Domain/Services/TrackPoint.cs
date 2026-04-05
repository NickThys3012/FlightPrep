namespace FlightPrep.Domain.Services;

/// <summary>A single GPS coordinate point on a flight track.</summary>
public record TrackPoint(double Lat, double Lon, double AltM);
