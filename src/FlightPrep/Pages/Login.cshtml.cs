using FlightPrep.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FlightPrep.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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
                ModelState.AddModelError(string.Empty, "Your account is pending admin approval.");
                return Page();
            }
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Account is vergrendeld wegens te veel mislukte pogingen. Probeer later opnieuw.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Ongeldige inloggegevens.");
        return Page();
    }
}
