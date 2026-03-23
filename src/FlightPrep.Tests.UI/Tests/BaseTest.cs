using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace FlightPrep.Tests.UI;

[TestFixture]
public abstract class BaseTest : PageTest
{
    protected const string BaseUrl = "http://localhost:8082";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        Locale = "nl-BE",
    };
}
