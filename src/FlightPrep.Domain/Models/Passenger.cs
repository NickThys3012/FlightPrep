namespace FlightPrep.Domain.Models;

public class Passenger
{
    public int Id { get; set; }
    public int FlightPreparationId { get; set; }
    public FlightPreparation FlightPreparation { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public double WeightKg { get; set; }
    public int Order { get; set; }

    // OFP manifest flags
    public bool IsChild         { get; set; }
    public bool NeedsAssistance { get; set; }
    public bool IsTransport     { get; set; }
}
