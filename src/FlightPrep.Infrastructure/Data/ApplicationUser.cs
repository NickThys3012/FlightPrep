using Microsoft.AspNetCore.Identity;

namespace FlightPrep.Infrastructure.Data;

public class ApplicationUser : IdentityUser
{
    public bool IsApproved { get; set; }
}
