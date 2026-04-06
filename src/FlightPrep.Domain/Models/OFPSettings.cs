namespace FlightPrep.Domain.Models;

public class OFPSettings
{
    public int     Id                         { get; set; }
    public string? UserId                     { get; set; }
    public double  PassengerEquipmentWeightKg { get; set; } = 7;
}
