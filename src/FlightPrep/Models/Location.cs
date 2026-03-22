namespace FlightPrep.Models;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IcaoCode { get; set; }
    public string? AirspaceNotes { get; set; }
}
