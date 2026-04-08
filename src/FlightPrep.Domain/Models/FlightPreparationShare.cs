namespace FlightPrep.Domain.Models;

public class FlightPreparationShare
{
    public int Id { get; set; }
    public int FlightPreparationId { get; set; }
    public string SharedWithUserId { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
}
