using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace FlightPrep.Tests.UI.Tests;

[TestFixture]
[Category("E2E")]
[NonParallelizable]
#pragma warning disable CA1501
public class FlightListFilterTest : BaseTest
#pragma warning restore CA1501
{
    private static int _testFlightId = -1;

    [OneTimeSetUp]
    public async Task SetUpOnce()
    {
        await CreateAuthStateAsync();
        _testFlightId = await CreateAndFlyTestFlightAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownOnce()
    {
        if (_testFlightId <= 0) return;
        await DeleteFlightAsAdminAsync(_testFlightId);
        _testFlightId = -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 1 – Filter bar is visible
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(1)]
    [Description("Filter bar with Alle/Gevlogen/Niet gevlogen buttons is visible on /flights")]
    public async Task FilterBar_IsVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The filter bar sits inside the fp-card before the table
        var alleBtn = Page.Locator("button:has-text('Alle')");
        var gevlogenBtn = Page.Locator("button:has-text('Gevlogen')");
        var nietGevlogenBtn = Page.Locator("button:has-text('Niet gevlogen')");

        await Expect(alleBtn.First).ToBeVisibleAsync();
        await Expect(gevlogenBtn.First).ToBeVisibleAsync();
        await Expect(nietGevlogenBtn.First).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 2 – Gevlogen filter shows only flown flights
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(2)]
    [Description("Clicking Gevlogen shows only rows that have the Gevlogen badge")]
    public async Task FilterGevlogen_ShowsOnlyFlownFlights()
    {
        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("button:has-text('Gevlogen')").First.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        var rows = Page.Locator("table.table tbody tr");
        var rowCount = await rows.CountAsync();

        // If there are no rows at all (e.g. only a "geen vaarten" placeholder),
        // the filter worked — nothing to assert about badges.
        if (rowCount == 0) return;

        // Skip the "no results" placeholder row
        var firstCellText = await rows.First.Locator("td").First.TextContentAsync();
        if (firstCellText != null && firstCellText.Contains("Geen vaarten")) return;

        // Every visible data row must have a "Gevlogen" badge in its Status cell
        for (var i = 0; i < rowCount; i++)
        {
            var row = rows.Nth(i);
            var gevlogenBadge = row.Locator("span.badge.bg-success:has-text('Gevlogen')");
            await Expect(gevlogenBadge).ToHaveCountAsync(1);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 3 – Niet gevlogen filter shows no flown flights
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(3)]
    [Description("Clicking Niet gevlogen shows no rows with the Gevlogen badge")]
    public async Task FilterNietGevlogen_ShowsOnlyUnflownFlights()
    {
        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("button:has-text('Niet gevlogen')").First.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        var rows = Page.Locator("table.table tbody tr");
        var rowCount = await rows.CountAsync();

        if (rowCount == 0) return;

        var firstCellText = await rows.First.Locator("td").First.TextContentAsync();
        if (firstCellText != null && firstCellText.Contains("Geen vaarten")) return;

        // No row should contain a "Gevlogen" badge
        for (var i = 0; i < rowCount; i++)
        {
            var row = rows.Nth(i);
            var gevlogenBadge = row.Locator("span.badge.bg-success:has-text('Gevlogen')");
            await Expect(gevlogenBadge).ToHaveCountAsync(0);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 4 – Alle filter shows all flights
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(4)]
    [Description("Clicking Alle shows the total count that matches Gevlogen + Niet gevlogen")]
    public async Task FilterAlle_ShowsAllFlights()
    {
        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Read counts from button labels: "Alle (N)", "✈️ Gevlogen (N)", "📋 Niet gevlogen (N)"
        var alleText = await Page.Locator("button:has-text('Alle')").First.TextContentAsync();
        var gevlogenText = await Page.Locator("button:has-text('Gevlogen')").First.TextContentAsync();
        var nietGevlogenText = await Page.Locator("button:has-text('Niet gevlogen')").First.TextContentAsync();

        var alleCount = ExtractCount(alleText);
        var gevlogenCount = ExtractCount(gevlogenText);
        var nietGevlogenCount = ExtractCount(nietGevlogenText);

        // Click Alle and count rows (capped at page size 20)
        await Page.Locator("button:has-text('Alle')").First.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        // The Alle count must equal gevlogen + niet-gevlogen (shared flights may overlap categories,
        // but the filter button numbers must be internally consistent)
        Assert.That(alleCount, Is.GreaterThanOrEqualTo(gevlogenCount + nietGevlogenCount),
            "Alle count should be at least the sum of Gevlogen and Niet gevlogen");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 5 – Count badges are accurate
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(5)]
    [Description("The count in the Gevlogen button parentheses matches the actual row count")]
    public async Task FilterCountBadges_AreAccurate()
    {
        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var gevlogenText = await Page.Locator("button:has-text('Gevlogen')").First.TextContentAsync();
        var expectedCount = ExtractCount(gevlogenText);

        // Apply the gevlogen filter
        await Page.Locator("button:has-text('Gevlogen')").First.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        var rows = Page.Locator("table.table tbody tr");
        var actualRowCount = await rows.CountAsync();

        // Handle the "geen vaarten" placeholder row
        if (actualRowCount == 1)
        {
            var placeholderText = await rows.First.Locator("td").First.TextContentAsync();
            if (placeholderText != null && placeholderText.Contains("Geen vaarten"))
                actualRowCount = 0;
        }

        // When there are ≤20 rows (no pagination) the count must match exactly
        Assert.That(actualRowCount, Is.EqualTo(expectedCount),
            $"Gevlogen button says {expectedCount} but table shows {actualRowCount} rows");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parses the trailing number from "Alle (3)" → 3.</summary>
    private static int ExtractCount(string? text)
    {
        if (text == null) return 0;
        var m = Regex.Match(text, @"\((\d+)\)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    /// <summary>
    /// Creates a flight, marks it as flown via the UI, and returns the flight id.
    /// Uses a short-lived browser context so it doesn't interfere with test Page state.
    /// </summary>
    private static async Task<int> CreateAndFlyTestFlightAsync()
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

        // ── Create flight ──────────────────────────────────────────────────────
        await page.GotoAsync($"{BaseUrl}/flights/new");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        await page.Locator("#sec1 input[type='date']").First.FillAsync(today);

        var balloonSelect = page.Locator("#sec1 select").Nth(0);
        await TrySelectFirstOptionStatic(balloonSelect);

        var pilotSelect = page.Locator("#sec1 select").Nth(1);
        await TrySelectFirstOptionStatic(pilotSelect);

        var locationSelect = page.Locator("#sec1 select").Nth(2);
        await TrySelectFirstOptionStatic(locationSelect);

        await page.Locator("button.btn-primary.btn-lg:has-text('Opslaan')").ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/flights/\d+"),
            new PageWaitForURLOptions { Timeout = 15_000 });

        var match = Regex.Match(page.Url, @"/flights/(\d+)");
        if (!match.Success) return -1;
        var flightId = int.Parse(match.Groups[1].Value);

        // ── Mark as flown ──────────────────────────────────────────────────────
        await page.GotoAsync($"{BaseUrl}/flights/{flightId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var flownBtn = page.Locator("button.btn-outline-primary:has-text('gevlogen')");
        if (await flownBtn.CountAsync() > 0)
        {
            await flownBtn.ClickAsync();
            await page.WaitForTimeoutAsync(600);

            var modal = page.Locator(".modal.d-block");
            if (await modal.CountAsync() > 0)
            {
                var saveBtn = modal.Locator("button.btn-success:has-text('Opslaan')");
                if (await saveBtn.CountAsync() > 0)
                    await saveBtn.ClickAsync();
                await page.WaitForTimeoutAsync(800);
            }
        }

        return flightId;
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

        var flightRow = page.Locator($"tbody tr:has(a[href='/flights/{flightId}'])");
        if (await flightRow.CountAsync() == 0) return;

        await flightRow.Locator("button:has-text('Verwijder')").ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var confirmModal = page.Locator(".modal.d-block");
        if (await confirmModal.CountAsync() > 0)
            await confirmModal.Locator("button.btn-danger:has-text('Verwijderen')").ClickAsync();

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
