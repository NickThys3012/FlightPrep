using FlightPrep.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FlightPrep.Pages;

public class RegisterModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, IsApproved = false, EmailConfirmed = false };

        var result = await userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Pilot");
            return RedirectToPage("/Login", new { registered = 1 });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "E-mailadres is verplicht.")]
        [EmailAddress(ErrorMessage = "Ongeldig e-mailadres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wachtwoord is verplicht.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Wachtwoord moet minimaal 8 tekens bevatten.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bevestig uw wachtwoord.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Wachtwoorden komen niet overeen.")]
        [Display(Name = "Bevestig wachtwoord")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
