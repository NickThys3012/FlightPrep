using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace FlightPrep.Tests.UI;

[TestFixture]
[Category("E2E")]
[NonParallelizable]
public class FlightFlowTest : BaseTest
{
    // Shared across ordered tests within the same fixture instance.
    private static int _createdFlightId = -1;

    [OneTimeSetUp]
    public async Task LoginBeforeAllTests()
    {
        await LoginAsync(E2EAdminEmail, E2EAdminPassword);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 1 – Home page
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(1)]
    [Description("Home page loads and shows FlightPrep branding")]
    public async Task HomePageLoads()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("FlightPrep", RegexOptions.IgnoreCase));

        // NavMenu brand link (uses GetByText to avoid mixing CSS + text pseudo-selectors)
        var brand = Page.GetByText("FlightPrep").First;
        await Expect(brand).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 2 – New-flight form renders
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(2)]
    [Description("New flight page loads with the accordion form")]
    public async Task NewFlightPageLoads()
    {
        await Page.GotoAsync($"{BaseUrl}/flights/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator("#flightAccordion")).ToBeVisibleAsync();
        // Section 1 is open by default (Bootstrap accordion-collapse show)
        await Expect(Page.Locator("#sec1")).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 3 – Create a flight (section 1)
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(3)]
    [Description("Fill section 1 (general info) and save to create a new flight")]
    public async Task CreateFlightWithBasicInfo()
    {
        await Page.GotoAsync($"{BaseUrl}/flights/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Date
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        await Page.Locator("#sec1 input[type='date']").First.FillAsync(today);

        // Time
        var timeInput = Page.Locator("#sec1 input[type='time']").First;
        if (await timeInput.CountAsync() > 0)
            await timeInput.FillAsync("08:00");

        // Balloon – pick the first non-blank option if available
        var balloonSelect = Page.Locator("#sec1 select").Nth(0);
        await TrySelectFirstOption(balloonSelect);

        // Pilot
        var pilotSelect = Page.Locator("#sec1 select").Nth(1);
        await TrySelectFirstOption(pilotSelect);

        // Location
        var locationSelect = Page.Locator("#sec1 select").Nth(2);
        await TrySelectFirstOption(locationSelect);

        // Save (💾 Opslaan)
        await Page.Locator("button.btn-primary.btn-lg:has-text('Opslaan')").ClickAsync();

        // After save the app redirects to /flights/{id} or /flights/{id}/edit
        await Page.WaitForURLAsync(
            new Regex(@"/flights/\d+"),
            new PageWaitForURLOptions { Timeout = 15_000 });

        var match = Regex.Match(Page.Url, @"/flights/(\d+)");
        Assert.That(match.Success, Is.True, "URL should contain a numeric flight id");
        _createdFlightId = int.Parse(match.Groups[1].Value);
        Assert.That(_createdFlightId, Is.GreaterThan(0), "Created flight id must be positive");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 4 – Flight list
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(4)]
    [Description("Flight list shows at least the newly created flight")]
    public async Task FlightListShowsCreatedFlight()
    {
        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var rows = Page.Locator("table.table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Flight list should contain at least one row");

        if (_createdFlightId > 0)
        {
            // The "Bekijken" link for our flight should be in the table
            var link = Page.Locator($"a[href='/flights/{_createdFlightId}']");
            await Expect(link.First).ToBeVisibleAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 5 – Meteo section
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(5)]
    [Description("Open section 2 (meteo) and fill surface wind speed")]
    public async Task FillMeteoSection()
    {
        if (_createdFlightId <= 0) Assert.Ignore("Requires a flight created in Order(3)");

        await Page.GotoAsync($"{BaseUrl}/flights/{_createdFlightId}/edit");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open section 2 via its accordion-button
        await Page.Locator("[data-bs-target='#sec2']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("#sec2")).ToBeVisibleAsync();

        // Surface wind speed (kt) – last number input in the static wind row
        var sec2 = Page.Locator("#sec2");
        var numberInputs = sec2.Locator("input[type='number']");
        var inputCount = await numberInputs.CountAsync();
        if (inputCount > 0)
        {
            // Surface wind direction is typically the first; speed is second
            await numberInputs.Nth(Math.Min(1, inputCount - 1)).FillAsync("8");
        }

        await Page.Locator("button.btn-primary.btn-lg:has-text('Opslaan')").ClickAsync();
        await Page.WaitForURLAsync(
            new Regex(@"/flights/\d+"),
            new PageWaitForURLOptions { Timeout = 10_000 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 6 – View page
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(6)]
    [Description("View page loads and shows today's date and PDF button")]
    public async Task ViewPageLoadsCorrectly()
    {
        if (_createdFlightId <= 0) Assert.Ignore("Requires a flight created in Order(3)");

        await Page.GotoAsync($"{BaseUrl}/flights/{_createdFlightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Today's date should appear formatted as dd/MM/yyyy
        var todayFormatted = DateTime.Today.ToString("dd/MM/yyyy");
        await Expect(Page.GetByText(todayFormatted).First).ToBeVisibleAsync();

        // Download PDF button
        await Expect(
            Page.Locator("button.btn-success:has-text('Download PDF')").First
        ).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 7 – PDF download
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(7)]
    [Description("Clicking the PDF button triggers a file download")]
    public async Task PdfDownloadButtonWorks()
    {
        if (_createdFlightId <= 0) Assert.Ignore("Requires a flight created in Order(3)");

        await Page.GotoAsync($"{BaseUrl}/flights/{_createdFlightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var downloadTask = Page.WaitForDownloadAsync();
        await Page.Locator("button.btn-success:has-text('Download PDF')").First.ClickAsync();

        var download = await downloadTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.That(
            download.SuggestedFilename,
            Does.Match(new Regex(@"\.pdf$", RegexOptions.IgnoreCase)),
            "Downloaded file should have a .pdf extension");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 8 – Mark as flown
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(8)]
    [Description("Mark as flown modal saves and shows the Vluchtverslag section")]
    public async Task MarkAsFlownWorks()
    {
        if (_createdFlightId <= 0) Assert.Ignore("Requires a flight created in Order(3)");

        await Page.GotoAsync($"{BaseUrl}/flights/{_createdFlightId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click "🛬 Markeer als gevlogen"
        await Page.Locator("button.btn-outline-primary:has-text('gevlogen')").ClickAsync();
        await Page.WaitForTimeoutAsync(600);

        // Inline Blazor modal appears with class "modal d-block"
        var modal = Page.Locator(".modal.d-block");
        await Expect(modal).ToBeVisibleAsync();

        // "Werkelijke landing" text input
        var landingInput = modal.Locator("input[type='text']").First;
        if (await landingInput.CountAsync() > 0)
            await landingInput.FillAsync("Testlandingplaats");

        // "Vluchtduur (min)" number input
        var durationInput = modal.Locator("input[type='number']").First;
        if (await durationInput.CountAsync() > 0)
            await durationInput.FillAsync("45");

        // "Opmerkingen" textarea
        var notesTextarea = modal.Locator("textarea").First;
        if (await notesTextarea.CountAsync() > 0)
            await notesTextarea.FillAsync("Mooie vlucht, geen bijzonderheden.");

        // Save the modal (btn-success "Opslaan" inside the modal)
        await modal.Locator("button.btn-success:has-text('Opslaan')").ClickAsync();
        await Page.WaitForTimeoutAsync(1_000);

        // After saving, the Vluchtverslag card should appear
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(
            Page.GetByText("Vluchtverslag").First
        ).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 9 – Settings pages
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(9)]
    [Description("All settings pages load without errors")]
    public async Task SettingsPagesLoad()
    {
        var paths = new[]
        {
            "/settings/balloons",
            "/settings/pilots",
            "/settings/locations",
            "/settings/gonogo",
        };

        foreach (var path in paths)
        {
            await Page.GotoAsync($"{BaseUrl}{path}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Should not end up on an error page
            await Expect(Page).Not.ToHaveTitleAsync(new Regex("Error", RegexOptions.IgnoreCase));
            Assert.That(Page.Url, Does.Contain(path), $"Should stay on {path}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Order 10 – Cleanup: delete the test flight
    // ─────────────────────────────────────────────────────────────────────────

    [Test, Order(10)]
    [Description("Delete the test flight created in Order(3)")]
    public async Task CleanupDeleteTestFlight()
    {
        if (_createdFlightId <= 0) Assert.Ignore("No test flight to clean up");

        await Page.GotoAsync($"{BaseUrl}/flights");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the table row that contains a link to our specific flight
        var flightRow = Page.Locator($"tbody tr:has(a[href='/flights/{_createdFlightId}'])");
        await Expect(flightRow).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        // Click the delete button in that row (🗑️ Verwijder)
        await flightRow.Locator("button:has-text('Verwijder')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Confirmation modal
        var confirmModal = Page.Locator(".modal.d-block");
        await Expect(confirmModal).ToBeVisibleAsync();
        await confirmModal.Locator("button.btn-danger:has-text('Verwijderen')").ClickAsync();

        await Page.WaitForTimeoutAsync(800);

        // The row should no longer be in the table
        var deletedRow = Page.Locator($"tbody tr:has(a[href='/flights/{_createdFlightId}'])");
        await Expect(deletedRow).ToHaveCountAsync(0);

        _createdFlightId = -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Selects the first non-blank option in a &lt;select&gt;, if any exist.</summary>
    private static async Task TrySelectFirstOption(ILocator select)
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
