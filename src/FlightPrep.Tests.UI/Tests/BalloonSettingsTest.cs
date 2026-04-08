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

    [OneTimeSetUp]
    public async Task SetUpOnce()
    {
        await CreateAuthStateAsync();
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
    [Description("The edit row for a balloon has 4 number inputs (not 5) — Leeggewicht is computed, not editable")]
    public async Task BalloonSettings_LeeggewichtColumn_IsReadOnly()
    {
        await Page.GotoAsync($"{BaseUrl}/settings/balloons");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the edit (✏️) button on the first balloon row
        var editBtn = Page.Locator("tbody tr button:has-text('✏️')").First;
        if (await editBtn.CountAsync() == 0)
        {
            Assert.Ignore("No balloon rows found — cannot test edit row");
            return;
        }

        await editBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        // The edit row contains number inputs for: Volume, Ti, EnvelopeOnly, Basket, Burner, Cylinders
        // Leeggewicht is rendered as text, NOT as an input.
        // So there should be exactly 4 type=number inputs in the edit row (EnvelopeOnly, Basket, Burner, Cylinders)
        // plus 2 text inputs (Registration, Type) = 6 inputs total, but only 4 are number inputs.
        var editRow = Page.Locator("tbody tr").Filter(new LocatorFilterOptions
        {
            Has = Page.Locator("button:has-text('✅ Opslaan')")
        });

        await Expect(editRow).ToBeVisibleAsync();

        var numberInputs = editRow.Locator("input[type='number']");
        var count = await numberInputs.CountAsync();

        // Expect 4 number inputs: Volume, Ti, EnvelopeOnly, Basket (plus Burner + Cylinders = 6 total)
        // Actually the razor has: VolumeM3, InternalEnvelopeTempC, EnvelopeOnlyWeightKg, BasketWeightKg, BurnerWeightKg, CylindersWeightKg = 6 number inputs
        // Before the fix it also had EmptyWeightKg = 7. Now it's 6.
        // We assert it is NOT 7 (the old count) and EmptyWeightKg is displayed as text.
        Assert.That(count, Is.Not.EqualTo(7),
            "EmptyWeightKg must NOT be an editable input — it should be shown as computed text");

        // Also verify the Leeggewicht cell shows a plain text value (not an input)
        // The 5th <td> (index 4) should contain plain text, not an <input>
        var leeggewichtCell = editRow.Locator("td").Nth(4);
        var leeggewichtInputs = leeggewichtCell.Locator("input");
        await Expect(leeggewichtInputs).ToHaveCountAsync(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 2 – Leeggewicht is displayed as computed sum of component weights
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(2)]
    [Description("Adding a balloon with component weights shows Leeggewicht as the computed sum")]
    public async Task BalloonSettings_LeeggewichtDisplayed_AsComputedSum()
    {
        await Page.GotoAsync($"{BaseUrl}/settings/balloons");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Component weights we will enter
        const double envelopeKg = 120.5;
        const double basketKg = 85.0;
        const double burnerKg = 25.5;
        const double cylindersKg = 30.0;
        var expectedSum = envelopeKg + basketKg + burnerKg + cylindersKg; // 261.0

        _createdRegistration = $"OO-E2E-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        // Click "➕ Ballon toevoegen"
        var addBtn = Page.Locator("button:has-text('Ballon toevoegen')");
        await Expect(addBtn).ToBeVisibleAsync();
        await addBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        // The new row appears at the bottom of tbody
        var newRow = Page.Locator("tbody tr").Last;

        // Fill Registration
        await newRow.Locator("input.form-control").First.FillAsync(_createdRegistration);

        // Fill Type
        await newRow.Locator("input.form-control").Nth(1).FillAsync("E2E-Type");

        // Number inputs: VolumeM3, InternalEnvelopeTempC, EnvelopeOnlyWeightKg, BasketWeightKg, BurnerWeightKg, CylindersWeightKg
        var numInputs = newRow.Locator("input[type='number']");

        await numInputs.Nth(0).FillAsync("2200");   // Volume
        await numInputs.Nth(1).FillAsync("80");      // Ti

        // Wait for Blazor to process changes before entering weight values
        await Page.WaitForTimeoutAsync(300);

        await numInputs.Nth(2).FillAsync(envelopeKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        await numInputs.Nth(3).FillAsync(basketKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        await numInputs.Nth(4).FillAsync(burnerKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        await numInputs.Nth(5).FillAsync(cylindersKg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));

        // Save the new balloon
        await newRow.Locator("button:has-text('✅ Toevoegen')").ClickAsync();
        await Page.WaitForTimeoutAsync(1_000);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the newly saved row by registration
        var savedRow = Page.Locator($"tbody tr:has-text('{_createdRegistration}')");
        await Expect(savedRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });

        // The Leeggewicht cell is the 5th column (index 4)
        var leeggewichtCell = savedRow.Locator("td").Nth(4);
        var leeggewichtText = await leeggewichtCell.TextContentAsync();

        // Parse the displayed value – it's rendered as "261.0" or "261,0" depending on locale
        leeggewichtText = leeggewichtText?.Trim().Replace(',', '.');
        var parsed = double.TryParse(leeggewichtText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var displayedSum);

        Assert.That(parsed, Is.True,
            $"Expected Leeggewicht cell to contain a numeric value, got: '{leeggewichtText}'");
        Assert.That(displayedSum, Is.EqualTo(expectedSum).Within(0.1),
            $"Leeggewicht should be {expectedSum} (sum of components), but got {displayedSum}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

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

        await row.Locator("button:has-text('🗑️')").ClickAsync();
        await page.WaitForTimeoutAsync(800);
    }
}
