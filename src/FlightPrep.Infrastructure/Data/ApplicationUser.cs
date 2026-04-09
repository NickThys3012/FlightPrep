using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FlightPrep.Infrastructure.Data;

public class ApplicationUser : IdentityUser
{
    public bool    IsApproved   { get; set; }
    [MaxLength(100)]
    public string? OperatorName { get; set; }
    public double? WeightKg     { get; set; }
    public string  PreferredLocale  { get; set; } = "nl-BE";
}
