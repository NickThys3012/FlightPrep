namespace FlightPrep.Domain.Models;

public class Pilot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? WeightKg { get; set; }
    public string? OwnerId { get; set; }
}
