using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace FlightPrep.Tests.UI.Tests;

[TestFixture]
public abstract class BaseTest : PageTest
{
    protected const string BaseUrl = "http://localhost:8082";

    // Credentials for the seeded admin account used by E2E tests.
    // Must match SEED_ADMIN_USERNAME / SEED_ADMIN_PASSWORD in docker-compose / CI.
    private static readonly string E2EAdminEmail =
        Environment.GetEnvironmentVariable("E2E_ADMIN_EMAIL") ?? "admin@e2etest.local";

    private static readonly string E2EAdminPassword =
        Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD") ?? "E2eTest_Admin_123!";

    // Populated by CreateAuthStateAsync() in [OneTimeSetUp] — loaded into every test context.
#pragma warning disable CA2211
    protected static string? AuthStatePath;
#pragma warning restore CA2211

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        Locale = "nl-BE",
        StorageStatePath = AuthStatePath,
    };

    /// <summary>
    /// Logs in using a dedicated short-lived Playwright instance and saves the resulting
    /// browser storage state (cookies) to a temp file. Call this from [OneTimeSetUp]
    /// in concrete fixtures. Subsequent test contexts automatically load the saved state
    /// via ContextOptions().StorageStatePath.
    /// </summary>
    protected static async Task CreateAuthStateAsync()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/Login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("input[name='Input.Email']").FillAsync(E2EAdminEmail);
        await page.Locator("input[name='Input.Password']").FillAsync(E2EAdminPassword);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        AuthStatePath = Path.Combine(Path.GetTempPath(), $"e2e-auth-{Guid.NewGuid()}.json");
        await context.StorageStateAsync(new() { Path = AuthStatePath });
    }
}
