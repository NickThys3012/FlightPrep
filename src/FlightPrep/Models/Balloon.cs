namespace FlightPrep.Models;

public class Balloon
{
    public int Id { get; set; }
    public string Registration { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Volume { get; set; } = string.Empty;
    public double? EmptyWeightKg { get; set; }
}
