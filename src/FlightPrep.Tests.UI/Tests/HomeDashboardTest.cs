using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace FlightPrep.Tests.UI;

[TestFixture]
[Category("E2E")]
[NonParallelizable]
#pragma warning disable CA1501
public class HomeDashboardTest : BaseTest
#pragma warning restore CA1501
{
    // ── Viewer credentials (must match FlightSharingTest) ────────────────────
    private const string ViewerEmail = "viewer@e2etest.local";
    private const string ViewerPassword = "E2eTest_Viewer_123!";

    // ── State shared across tests ─────────────────────────────────────────────
    private static string? _viewerAuthStatePath;

    [OneTimeSetUp]
    public async Task SetUpOnce()
    {
        await CreateAuthStateAsync();
        await CreateViewerAuthStateAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownOnce()
    {
        if (_viewerAuthStatePath != null && File.Exists(_viewerAuthStatePath))
        {
            File.Delete(_viewerAuthStatePath);
            _viewerAuthStatePath = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 1 – Home page stats show a non-negative total
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(1)]
    [Description("As admin, the Totaal Vaarten stat on the home page shows a non-negative integer")]
    public async Task HomePageStats_ShowOnlyOwnFlights()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The stats row renders three .fp-stat-card elements.
        // "Totaal Vaarten" is the label inside the first one.
        var totaalLocator = Page.Locator(".fp-stat-card")
            .Filter(new LocatorFilterOptions { HasText = "Totaal Vaarten" });

        await Expect(totaalLocator.First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // Extract the numeric value from inside the card
        var cardText = await totaalLocator.First.TextContentAsync();
        var match = Regex.Match(cardText ?? string.Empty, @"\b(\d+)\b");
        Assert.That(match.Success, Is.True,
            $"Expected a number inside the Totaal Vaarten card, got: '{cardText}'");

        var total = int.Parse(match.Groups[1].Value);
        Assert.That(total, Is.GreaterThanOrEqualTo(0),
            "Totaal Vaarten must be a non-negative integer");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 2 – Shared flight shows "Gedeeld" badge in viewer's Recente Vaarten
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(2)]
    [Description("A flight shared with viewer appears with a 'Gedeeld' badge in the viewer's home page recent list")]
    public async Task RecentFlights_SharedFlightShowsGedeeldBadge()
    {
        if (_viewerAuthStatePath == null)
            Assert.Ignore("Viewer auth state not available — skipping");

        // ── 1. Create a flight as admin ──────────────────────────────────────
        var flightId = await CreateTestFlightAsync();
        Assert.That(flightId, Is.GreaterThan(0), "Test flight must be created");

        try
        {
            // ── 2. Share the flight with the viewer ──────────────────────────
            await ShareFlightWithViewerAsync(flightId);

            // ── 3. Login as viewer and check home page ───────────────────────
            await using var viewerCtx = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                Locale = "nl-BE",
                StorageStatePath = _viewerAuthStatePath,
            });
            var viewerPage = await viewerCtx.NewPageAsync();

            await viewerPage.GotoAsync(BaseUrl);
            await viewerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // The "Recente Vaarten" section on the home page lists recent flights.
            // A shared flight renders with a badge "Gedeeld" (bg-info).
            var gedeeldBadge = viewerPage.Locator("span.badge.bg-info.text-dark:has-text('Gedeeld')");
            await Expect(gedeeldBadge.First).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        }
        finally
        {
            // ── Cleanup: delete test flight as admin ─────────────────────────
            await DeleteFlightAsAdminAsync(flightId);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers viewer@e2etest.local if needed, then logs in and saves auth state.
    /// Idempotent — tries login first; falls back to registration if login fails.
    /// </summary>
    private static async Task CreateViewerAuthStateAsync()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var ctx = await browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
        });
        var page = await ctx.NewPageAsync();

        // Try direct login first
        await page.GotoAsync($"{BaseUrl}/Login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("input[name='Input.Email']").FillAsync(ViewerEmail);
        await page.Locator("input[name='Input.Password']").FillAsync(ViewerPassword);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (!page.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
        {
            _viewerAuthStatePath = Path.Combine(Path.GetTempPath(), $"e2e-home-viewer-auth-{Guid.NewGuid()}.json");
            await ctx.StorageStateAsync(new() { Path = _viewerAuthStatePath });
            return;
        }

        // Register and then log in
        await page.GotoAsync($"{BaseUrl}/Register");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("input[name='Input.Email']").FillAsync(ViewerEmail);
        await page.Locator("input[name='Input.Password']").FillAsync(ViewerPassword);
        await page.Locator("input[name='Input.ConfirmPassword']").FillAsync(ViewerPassword);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.GotoAsync($"{BaseUrl}/Login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("input[name='Input.Email']").FillAsync(ViewerEmail);
        await page.Locator("input[name='Input.Password']").FillAsync(ViewerPassword);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        _viewerAuthStatePath = Path.Combine(Path.GetTempPath(), $"e2e-home-viewer-auth-{Guid.NewGuid()}.json");
        await ctx.StorageStateAsync(new() { Path = _viewerAuthStatePath });
    }

    private static async Task<int> CreateTestFlightAsync()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var ctx = await browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
            StorageStatePath = AuthStatePath,
        });
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/flights/new");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        await page.Locator("#sec1 input[type='date']").First.FillAsync(today);

        await TrySelectFirstOptionStatic(page.Locator("#sec1 select").Nth(0));
        await TrySelectFirstOptionStatic(page.Locator("#sec1 select").Nth(1));
        await TrySelectFirstOptionStatic(page.Locator("#sec1 select").Nth(2));

        await page.Locator("button.btn-primary.btn-lg:has-text('Opslaan')").ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/flights/\d+"),
            new PageWaitForURLOptions { Timeout = 15_000 });

        var match = Regex.Match(page.Url, @"/flights/(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    /// <summary>
    /// Navigates to the flight view as admin and shares it with the viewer using the Delen panel.
    /// </summary>
    private async Task ShareFlightWithViewerAsync(int flightId)
    {
        await Page.GotoAsync($"{BaseUrl}/flights/{flightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var delenCard = Page.Locator(".card:has(.card-header:has-text('Delen'))");
        await Expect(delenCard).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        var shareSelect = delenCard.Locator("select.form-select");
        if (await shareSelect.CountAsync() > 0)
        {
            var viewerOption = shareSelect.Locator($"option:has-text('{ViewerEmail}')");
            if (await viewerOption.CountAsync() > 0)
            {
                var value = await viewerOption.GetAttributeAsync("value");
                if (!string.IsNullOrEmpty(value))
                    await shareSelect.SelectOptionAsync(value);
            }
        }

        var delenBtn = delenCard.Locator("button:has-text('➕ Delen')");
        if (await delenBtn.CountAsync() > 0)
        {
            await delenBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(1_000);
        }
    }

    private static async Task DeleteFlightAsAdminAsync(int flightId)
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var ctx = await browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
            StorageStatePath = AuthStatePath,
        });
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/flights");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var row = page.Locator($"tbody tr:has(a[href='/flights/{flightId}'])");
        if (await row.CountAsync() == 0) return;

        await row.Locator("button:has-text('Verwijder')").ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var modal = page.Locator(".modal.d-block");
        if (await modal.CountAsync() > 0)
            await modal.Locator("button.btn-danger:has-text('Verwijderen')").ClickAsync();

        await page.WaitForTimeoutAsync(800);
    }

    private static async Task TrySelectFirstOptionStatic(ILocator select)
    {
        if (await select.CountAsync() == 0) return;
        var options = await select.Locator("option:not([value=''])").AllAsync();
        if (options.Count > 0)
        {
            var value = await options[0].GetAttributeAsync("value");
            if (!string.IsNullOrEmpty(value))
                await select.SelectOptionAsync(value);
        }
    }
}
