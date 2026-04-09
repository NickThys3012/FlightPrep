using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace FlightPrep.Tests.UI.Tests;

[TestFixture]
[Category("E2E")]
[NonParallelizable]
#pragma warning disable CA1501
public class FlightSharingTest : BaseTest
#pragma warning restore CA1501
{
    // ── Viewer credentials (must match SEED_VIEWER_USERNAME / SEED_VIEWER_PASSWORD) ──
    private static readonly string ViewerEmail =
        Environment.GetEnvironmentVariable("E2E_VIEWER_EMAIL") ?? "viewer@e2etest.local";
    private static readonly string ViewerPassword =
        Environment.GetEnvironmentVariable("E2E_VIEWER_PASSWORD") ?? "E2eTest_Viewer_123!";

    // ── Shared state across ordered tests ─────────────────────────────────────
    private static int _testFlightId = -1;
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
        // Best-effort cleanup: delete test flight as admin
        if (_testFlightId > 0)
        {
            await DeleteFlightAsAdminAsync(_testFlightId);
            _testFlightId = -1;
        }

        // Clean up temp auth state file
        if (_viewerAuthStatePath != null && File.Exists(_viewerAuthStatePath))
        {
            File.Delete(_viewerAuthStatePath);
            _viewerAuthStatePath = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 1 – Register viewer user and create a test flight
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(1)]
    [Description("Register viewer@e2etest.local (if not yet registered) and create a flight as admin")]
    public async Task Setup_CreateViewerUserAndFlight()
    {
        // Viewer registration (idempotent — may already exist from a prior run)
        await EnsureViewerRegisteredAsync();

        // Create test flight as admin
        _testFlightId = await CreateTestFlightAsync();
        Assert.That(_testFlightId, Is.GreaterThan(0), "Test flight must be created successfully");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 2 – Share panel is visible to owner
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(2)]
    [Description("The 🔗 Delen card is visible when the owner views their flight")]
    public async Task SharePanel_VisibleToOwner()
    {
        if (_testFlightId <= 0) Assert.Ignore("Requires a flight created in Order(1)");

        await Page.GotoAsync($"{BaseUrl}/flights/{_testFlightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The Delen card header
        var delenHeader = Page.Locator(".card-header:has-text('Delen')");
        await Expect(delenHeader.First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 3 – Admin shares the flight with viewer; viewer appears in shares list
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(3)]
    [Description("Admin shares the flight with viewer@e2etest.local using the Delen panel")]
    public async Task ShareFlight_ViewerAppearsInSharesList()
    {
        if (_testFlightId <= 0) Assert.Ignore("Requires a flight created in Order(1)");

        await Page.GotoAsync($"{BaseUrl}/flights/{_testFlightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the Delen section to render
        var delenCard = Page.Locator(".card:has(.card-header:has-text('Delen'))");
        await Expect(delenCard).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // Find the share dropdown — select viewer by email/username if multiple options exist
        var shareSelect = delenCard.Locator("select.form-select");
        var optionCount = await shareSelect.Locator("option").CountAsync();

        if (optionCount > 0)
        {
            // Try to select the viewer option
            var viewerOption = shareSelect.Locator($"option:has-text('{ViewerEmail}')");
            if (await viewerOption.CountAsync() > 0)
            {
                var value = await viewerOption.GetAttributeAsync("value");
                if (!string.IsNullOrEmpty(value))
                    await shareSelect.SelectOptionAsync(value);
            }
            // If the dropdown already has the correct user selected (only option), proceed.
        }

        // Click "➕ Delen"
        var delenBtn = delenCard.Locator("button:has-text('➕ Delen')");
        await Expect(delenBtn).ToBeVisibleAsync();
        await delenBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1_000);

        // Viewer should now appear in the shares list
        var sharesList = delenCard.Locator(".list-group");
        await Expect(sharesList.Locator($"li:has-text('{ViewerEmail}')")).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 4 – Shared flight appears in viewer's flight list
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(4)]
    [Description("The shared flight appears in the viewer's /flights list with a 'Gedeeld door' badge")]
    public async Task SharedFlight_AppearsInViewerList()
    {
        if (_testFlightId <= 0 || _viewerAuthStatePath == null)
            Assert.Ignore("Requires flight and viewer auth state from Order(1-3)");

        await using var viewerCtx = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
            StorageStatePath = _viewerAuthStatePath,
        });
        var viewerPage = await viewerCtx.NewPageAsync();

        await viewerPage.GotoAsync($"{BaseUrl}/flights");
        await viewerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The shared flight row should have a "Gedeeld door" badge
        var sharedBadge = viewerPage.Locator("span.badge.bg-secondary:has-text('Gedeeld door')");
        await Expect(sharedBadge.First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 5 – Viewer cannot edit the shared flight
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(5)]
    [Description("As viewer, the Bewerken button is absent and the shared info banner is visible")]
    public async Task SharedFlight_ViewerCannotEdit()
    {
        if (_testFlightId <= 0 || _viewerAuthStatePath == null)
            Assert.Ignore("Requires flight and viewer auth state from prior orders");

        await using var viewerCtx = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
            StorageStatePath = _viewerAuthStatePath,
        });
        var viewerPage = await viewerCtx.NewPageAsync();

        await viewerPage.GotoAsync($"{BaseUrl}/flights/{_testFlightId}");
        await viewerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The blue info alert should be visible
        var infoBanner = viewerPage.Locator(".alert.alert-info");
        await Expect(infoBanner).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // The "Bewerken" edit link must NOT be visible for shared viewers
        var bewerkenLink = viewerPage.Locator($"a[href='/flights/{_testFlightId}/edit']");
        await Expect(bewerkenLink).ToHaveCountAsync(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 6 – Viewer cannot delete the shared flight
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(6)]
    [Description("As viewer, no delete button is shown for the shared flight row")]
    public async Task SharedFlight_ViewerCannotDelete()
    {
        if (_testFlightId <= 0 || _viewerAuthStatePath == null)
            Assert.Ignore("Requires flight and viewer auth state from prior orders");

        await using var viewerCtx = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
            StorageStatePath = _viewerAuthStatePath,
        });
        var viewerPage = await viewerCtx.NewPageAsync();

        await viewerPage.GotoAsync($"{BaseUrl}/flights");
        await viewerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the shared flight row by the flight id link
        var sharedRow = viewerPage.Locator($"tbody tr:has(a[href='/flights/{_testFlightId}'])");
        await Expect(sharedRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // No "Verwijder" button in that row
        var deleteBtn = sharedRow.Locator("button:has-text('Verwijder')");
        await Expect(deleteBtn).ToHaveCountAsync(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 7 – Revoking share removes flight from viewer's list
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(7)]
    [Description("After admin revokes the share, the flight disappears from viewer's flight list")]
    public async Task RevokeShare_FlightDisappearsFromViewerList()
    {
        if (_testFlightId <= 0 || _viewerAuthStatePath == null)
            Assert.Ignore("Requires flight and viewer auth state from prior orders");

        // ── Admin revokes the share ────────────────────────────────────────────
        await Page.GotoAsync($"{BaseUrl}/flights/{_testFlightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var delenCard = Page.Locator(".card:has(.card-header:has-text('Delen'))");
        await Expect(delenCard).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // Click the ❌ Verwijderen button next to the viewer in the shares list
        var revokeBtn = delenCard.Locator($"li:has-text('{ViewerEmail}') button:has-text('❌ Verwijderen')");
        await Expect(revokeBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 6_000 });
        await revokeBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1_000);

        // ── Viewer verifies the flight is gone ────────────────────────────────
        await using var viewerCtx = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "nl-BE",
            StorageStatePath = _viewerAuthStatePath,
        });
        var viewerPage = await viewerCtx.NewPageAsync();

        await viewerPage.GotoAsync($"{BaseUrl}/flights");
        await viewerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var sharedRow = viewerPage.Locator($"tbody tr:has(a[href='/flights/{_testFlightId}'])");
        await Expect(sharedRow).ToHaveCountAsync(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 8 – Cleanup
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(8)]
    [Description("Delete the test flight created in Order(1)")]
    public async Task Cleanup()
    {
        if (_testFlightId <= 0) Assert.Ignore("No test flight to clean up");

        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var flightRow = Page.Locator($"tbody tr:has(a[href='/flights/{_testFlightId}'])");
        if (await flightRow.CountAsync() == 0)
        {
            _testFlightId = -1;
            return; // Already gone
        }

        await flightRow.Locator("button:has-text('Verwijder')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var confirmModal = Page.Locator(".modal.d-block");
        await Expect(confirmModal).ToBeVisibleAsync();
        await confirmModal.Locator("button.btn-danger:has-text('Verwijderen')").ClickAsync();
        await Page.WaitForTimeoutAsync(800);

        var deletedRow = Page.Locator($"tbody tr:has(a[href='/flights/{_testFlightId}'])");
        await Expect(deletedRow).ToHaveCountAsync(0);

        _testFlightId = -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Logs in as the seeded viewer account and saves the auth state to a temp file.
    /// The viewer is seeded at startup (SEED_VIEWER_USERNAME / SEED_VIEWER_PASSWORD)
    /// with IsApproved = true, so no UI registration is needed.
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

        await page.GotoAsync($"{BaseUrl}/Login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("input[name='Input.Email']").FillAsync(ViewerEmail);
        await page.Locator("input[name='Input.Password']").FillAsync(ViewerPassword);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(page.Url, Does.Not.Contain("/Login"),
            $"Viewer login failed — ensure SEED_VIEWER_USERNAME={ViewerEmail} is seeded with IsApproved=true");

        _viewerAuthStatePath = Path.Combine(Path.GetTempPath(), $"e2e-viewer-auth-{Guid.NewGuid()}.json");
        await ctx.StorageStateAsync(new() { Path = _viewerAuthStatePath });
    }

    /// <summary>Registers the viewer in a page context that is already set up (idempotent guard).</summary>
    private async Task EnsureViewerRegisteredAsync()
    {
        // The viewer auth state was already set up in [OneTimeSetUp] via CreateViewerAuthStateAsync().
        // This test step is kept as an explicit order marker and assertion.
        Assert.That(_viewerAuthStatePath, Is.Not.Null,
            "Viewer auth state should have been created in [OneTimeSetUp]");
        Assert.That(File.Exists(_viewerAuthStatePath), Is.True,
            $"Viewer auth state file should exist at {_viewerAuthStatePath}");
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
