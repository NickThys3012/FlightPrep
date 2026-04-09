using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace FlightPrep.Tests.UI;

[TestFixture]
[Category("E2E")]
[NonParallelizable]
#pragma warning disable CA1501
public class BalloonSettingsTest : BaseTest
#pragma warning restore CA1501
{
    // Keeps track of the registration we add so we can clean it up.
    private static string? _createdRegistration;

    // Component weights used in setup — shared between the two ordered tests.
    private const double EnvelopeKg = 120.5;
    private const double BasketKg = 85.0;
    private const double BurnerKg = 25.5;
    private const double CylindersKg = 30.0;
    private const double ExpectedSum = EnvelopeKg + BasketKg + BurnerKg + CylindersKg; // 261.0

    [OneTimeSetUp]
    public async Task SetUpOnce()
    {
        await CreateAuthStateAsync();

        // Create a dedicated test balloon so both ordered tests have a known row to work with.
        // This makes the fixture self-contained — no dependency on seed data.
        _createdRegistration = $"OO-E2E-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        await CreateTestBalloonAsync(_createdRegistration);
    }

    [OneTimeTearDown]
    public async Task TearDownOnce()
    {
        if (_createdRegistration == null) return;

        // Best-effort: delete the balloon we added during the test.
        await DeleteBalloonByRegistrationAsync(_createdRegistration);
        _createdRegistration = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 1 – Leeggewicht column is read-only (computed) in edit row
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(1)]
    [Description("The edit row for a balloon has 6 number inputs (not 7) — Leeggewicht is computed, not editable")]
    public async Task BalloonSettings_LeeggewichtColumn_IsReadOnly()
    {
        Assert.That(_createdRegistration, Is.Not.Null, "Test balloon must be created in [OneTimeSetUp]");

        await Page.GotoAsync($"{BaseUrl}/settings/balloons");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find our test balloon row and click its edit button (btn-outline-secondary, not emoji text)
        var testRow = Page.Locator($"tbody tr:has-text('{_createdRegistration}')");
        await Expect(testRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        var editBtn = testRow.Locator("button.btn-outline-secondary").First;
        await editBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        // Locate the edit row — it contains the save button
        var editRow = Page.Locator("tbody tr").Filter(new LocatorFilterOptions
        {
            Has = Page.Locator("button:has-text('Opslaan')")
        });
        await Expect(editRow).ToBeVisibleAsync();

        // VolumeM3, InternalEnvelopeTempC, EnvelopeOnlyWeightKg, BasketWeightKg, BurnerWeightKg, CylindersWeightKg = 6
        // Before the fix EmptyWeightKg was also editable = 7. Now it must be 6.
        var numberInputs = editRow.Locator("input[type='number']");
        var count = await numberInputs.CountAsync();
        Assert.That(count, Is.EqualTo(6),
            $"Expected 6 number inputs (EmptyWeightKg must be read-only), found {count}");

        // The Leeggewicht cell (5th column, index 4) must contain no input element
        var leeggewichtCell = editRow.Locator("td").Nth(4);
        await Expect(leeggewichtCell.Locator("input")).ToHaveCountAsync(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 2 – Leeggewicht is displayed as computed sum of component weights
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(2)]
    [Description("A balloon saved with component weights shows Leeggewicht as the computed sum")]
    public async Task BalloonSettings_LeeggewichtDisplayed_AsComputedSum()
    {
        Assert.That(_createdRegistration, Is.Not.Null, "Test balloon must be created in [OneTimeSetUp]");

        await Page.GotoAsync($"{BaseUrl}/settings/balloons");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the row created in [OneTimeSetUp] — Leeggewicht should already be computed
        var savedRow = Page.Locator($"tbody tr:has-text('{_createdRegistration}')");
        await Expect(savedRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // The Leeggewicht cell is the 5th column (index 4)
        var leeggewichtCell = savedRow.Locator("td").Nth(4);
        var leeggewichtText = (await leeggewichtCell.TextContentAsync())?.Trim().Replace(',', '.');

        var parsed = double.TryParse(leeggewichtText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var displayedSum);

        Assert.That(parsed, Is.True,
            $"Expected Leeggewicht cell to contain a numeric value, got: '{leeggewichtText}'");
        Assert.That(displayedSum, Is.EqualTo(ExpectedSum).Within(0.1),
            $"Leeggewicht should be {ExpectedSum} (sum of components), but got {displayedSum}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task CreateTestBalloonAsync(string registration)
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

        await page.GotoAsync($"{BaseUrl}/settings/balloons");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var addBtn = page.Locator("button:has-text('Ballon toevoegen')");
        await addBtn.ClickAsync();
        await page.WaitForTimeoutAsync(600);

        var newRow = page.Locator("tbody tr").Last;

        await newRow.Locator("input.form-control").First.FillAsync(registration);
        await newRow.Locator("input.form-control").Nth(1).FillAsync("E2E-Type");

        var numInputs = newRow.Locator("input[type='number']");
        await numInputs.Nth(0).FillAsync("2200");
        await numInputs.Nth(1).FillAsync("80");
        await page.WaitForTimeoutAsync(300);
        await numInputs.Nth(2).FillAsync(EnvelopeKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        await numInputs.Nth(3).FillAsync(BasketKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        await numInputs.Nth(4).FillAsync(BurnerKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        await numInputs.Nth(5).FillAsync(CylindersKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));

        await newRow.Locator("button:has-text('Toevoegen')").ClickAsync();
        await page.WaitForTimeoutAsync(1_000);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task DeleteBalloonByRegistrationAsync(string registration)
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

        await page.GotoAsync($"{BaseUrl}/settings/balloons");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var row = page.Locator($"tbody tr:has-text('{registration}')");
        if (await row.CountAsync() == 0) return;

        await row.Locator("button.btn-outline-danger").ClickAsync();
        await page.WaitForTimeoutAsync(800);
    }
}
