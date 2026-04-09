using Microsoft.AspNetCore.Identity;

namespace FlightPrep.Infrastructure.Data;

public class ApplicationUser : IdentityUser
{
    public bool    IsApproved       { get; set; }
    public string? OperatorName     { get; set; }
    public double? WeightKg         { get; set; }
    public string  PreferredLocale  { get; set; } = "nl-BE";
}
