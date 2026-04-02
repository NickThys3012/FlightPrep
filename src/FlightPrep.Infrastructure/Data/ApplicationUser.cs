using Microsoft.AspNetCore.Identity;

namespace FlightPrep.Data;

public class ApplicationUser : IdentityUser
{
    public bool IsApproved { get; set; } = false;
}
