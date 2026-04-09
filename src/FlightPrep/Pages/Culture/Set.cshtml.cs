using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FlightPrep.Infrastructure.Data;

namespace FlightPrep.Pages.Culture;

public class SetModel : PageModel
{
    private static readonly string[] AllowedCultures = ["nl-BE", "en-GB"];
    private readonly UserManager<ApplicationUser> _userManager;

    public SetModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "redirectUri is a query-string value bound by model binding; changing to Uri would break the Razor Page route.")]
    public async Task<IActionResult> OnGetAsync(string culture, string redirectUri)
    {
        if (!AllowedCultures.Contains(culture))
            culture = "nl-BE";

        if (string.IsNullOrEmpty(redirectUri) || !Url.IsLocalUrl(redirectUri))
            redirectUri = "/";

        // Set culture cookie
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, Secure = true, SameSite = SameSiteMode.Lax }
        );

        // Persist to user profile
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            user.PreferredLocale = culture;
            await _userManager.UpdateAsync(user);
        }

        return LocalRedirect(redirectUri);
    }
}
