using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace FlightPrep.Tests.UI;

[TestFixture]
public abstract class BaseTest : PageTest
{
    protected const string BaseUrl = "http://localhost:8082";

    // Credentials for the seeded admin account used by E2E tests.
    // Must match SEED_ADMIN_USERNAME / SEED_ADMIN_PASSWORD in docker-compose / CI.
    protected static readonly string E2EAdminEmail =
        Environment.GetEnvironmentVariable("E2E_ADMIN_EMAIL") ?? "admin@e2etest.local";
    protected static readonly string E2EAdminPassword =
        Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD") ?? "E2eTest_Admin_123!";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        Locale = "nl-BE",
    };

    /// <summary>
    /// Logs in via the Login Razor Page and waits for the redirect to complete.
    /// </summary>
    protected async Task LoginAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("input[name='Input.Email']").FillAsync(email);
        await Page.Locator("input[name='Input.Password']").FillAsync(password);
        await Page.Locator("button[type='submit']").ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
