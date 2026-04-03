using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FlightPrep.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<LoginModel> logger,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _dbFactory = dbFactory;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public bool Registered { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "E-mailadres is verplicht.")]
        [EmailAddress(ErrorMessage = "Ongeldig e-mailadres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wachtwoord is verplicht.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Onthoud mij")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null, int registered = 0)
    {
        ReturnUrl = returnUrl;
        Registered = registered == 1;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

        if (!ModelState.IsValid)
            return Page();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Validate password first — prevents email enumeration via the IsApproved pre-check.
        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Belt-and-suspenders: enforce IsApproved AFTER password succeeds.
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user is { IsApproved: false })
            {
                await _signInManager.SignOutAsync(); // revoke just-issued cookie
                _logger.LogWarning("Login attempt by unapproved user {Email} from {IpAddress}",
                    Input.Email, ip);
                RecordLoginEvent(Input.Email, user?.Id, false, "NotApproved", ip);
                ModelState.AddModelError(string.Empty, "Your account is pending admin approval.");
                return Page();
            }

            _logger.LogInformation("User {Email} logged in successfully from {IpAddress}",
                Input.Email, ip);
            RecordLoginEvent(Input.Email, user?.Id, true, null, ip);
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} account locked out (login attempt from {IpAddress})",
                Input.Email, ip);
            RecordLoginEvent(Input.Email, null, false, "LockedOut", ip);
            ModelState.AddModelError(string.Empty,
                "Account is vergrendeld wegens te veel mislukte pogingen. Probeer later opnieuw.");
            return Page();
        }

        _logger.LogWarning("Failed login attempt for {Email} from {IpAddress}",
            Input.Email, ip);
        RecordLoginEvent(Input.Email, null, false, "InvalidPassword", ip);
        ModelState.AddModelError(string.Empty, "Ongeldige inloggegevens.");
        return Page();
    }

    private void RecordLoginEvent(string email, string? userId, bool success, string? failureReason, string? ipAddress)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                db.LoginEvents.Add(new LoginEvent
                {
                    Email = email,
                    UserId = userId,
                    Success = success,
                    IpAddress = ipAddress,
                    FailureReason = failureReason,
                    Timestamp = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record login event for {Email}", email);
            }
        });
    }
}
