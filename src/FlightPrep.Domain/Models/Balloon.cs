namespace FlightPrep.Models;

public class Balloon
{
    public int Id { get; set; }
    public string Registration { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double? VolumeM3 { get; set; }
    public double? InternalEnvelopeTempC { get; set; }
    public double? EmptyWeightKg { get; set; }
}
