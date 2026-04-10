namespace FlightPrep.Domain.Models;

public class Balloon
{
    public int Id { get; set; }
    public string Registration { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double? VolumeM3 { get; set; }
    public double? InternalEnvelopeTempC { get; set; }
    public double? EmptyWeightKg =>
        (EnvelopeOnlyWeightKg ?? 0) + (BasketWeightKg ?? 0) + (BurnerWeightKg ?? 0) + (CylindersWeightKg ?? 0) > 0
            ? (EnvelopeOnlyWeightKg ?? 0) + (BasketWeightKg ?? 0) + (BurnerWeightKg ?? 0) + (CylindersWeightKg ?? 0)
            : null;
    public string? OwnerId { get; set; }

    // OFP reference weights (defaults; never used at PDF generation time — OFP snapshots are stored on FlightPreparation)
    public double? EnvelopeOnlyWeightKg { get; set; }
    public double? BasketWeightKg       { get; set; }
    public double? BurnerWeightKg       { get; set; }
    public double? CylindersWeightKg    { get; set; }
}
