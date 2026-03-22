namespace FlightPrep.Models;

/// <summary>Singleton row (Id=1) storing configurable Go/No-Go thresholds.</summary>
public class GoNoGoSettings
{
    public int Id { get; set; } = 1;

    // Wind speed thresholds (kt)
    public double WindYellowKt  { get; set; } = 10;
    public double WindRedKt     { get; set; } = 15;

    // Visibility thresholds (km)
    public double VisYellowKm   { get; set; } = 5;
    public double VisRedKm      { get; set; } = 3;

    // CAPE thresholds (J/kg)
    public double CapeYellowJkg { get; set; } = 300;
    public double CapeRedJkg    { get; set; } = 500;
}
