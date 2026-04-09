using FlightPrep.Infrastructure.Data;
using FlightPrep.Pages.Culture;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace FlightPrep.Tests;

/// <summary>
///     Unit tests for <see cref="SetModel.OnGetAsync" /> and for the
///     <see cref="ApplicationUser.PreferredLocale" /> default / setter.
/// </summary>
public class SetModelTests
{
    // ── Mock helpers ──────────────────────────────────────────────────────────

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    /// <summary>
    ///     Builds a <see cref="SetModel" /> wired with a real <see cref="DefaultHttpContext" />
    ///     (cookies written to response headers), a URL-helper mock, and the supplied
    ///     user-manager / logger mocks.
    /// </summary>
    private static (SetModel model, DefaultHttpContext httpContext)
        BuildSut(
            Mock<UserManager<ApplicationUser>> userManagerMock,
            ILogger<SetModel> logger)
    {
        var model = new SetModel(userManagerMock.Object, logger);

        var httpContext = new DefaultHttpContext();

        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            modelState);

        model.PageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper
            .Setup(u => u.IsLocalUrl(It.Is<string?>(s => s != null && s.StartsWith("/") && !s.StartsWith("//"))))
            .Returns(true);
        urlHelper
            .Setup(u => u.IsLocalUrl(It.Is<string?>(s => s == null || !s.StartsWith("/") || s.StartsWith("//"))))
            .Returns(false);
        model.Url = urlHelper.Object;

        return (model, httpContext);
    }

    /// <summary>
    ///     Extracts all Set-Cookie header values from the response after the handler ran.
    /// </summary>
    private static IList<string> GetSetCookieHeaders(DefaultHttpContext httpContext)
        => httpContext.Response.Headers.SetCookie.ToList();

    // ═════════════════════════════════════════════════════════════════════════
    // Culture validation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnGetAsync_InvalidCulture_FallsBackToNlBE()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, httpContext) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync("fr-FR", "/");

        // Assert — cookie value must contain "nl-BE"
        var cookies = GetSetCookieHeaders(httpContext);
        Assert.Contains(cookies, c => c.Contains("nl-BE"));
    }

    [Fact]
    public async Task OnGetAsync_NullCulture_FallsBackToNlBE()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, httpContext) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync(null!, "/");

        // Assert
        var cookies = GetSetCookieHeaders(httpContext);
        Assert.Contains(cookies, c => c.Contains("nl-BE"));
    }

    [Fact]
    public async Task OnGetAsync_ValidCultureNlBE_SetsCookieWithNlBE()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, httpContext) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync("nl-BE", "/");

        // Assert
        var cookies = GetSetCookieHeaders(httpContext);
        Assert.Contains(cookies, c => c.Contains("nl-BE"));
    }

    [Fact]
    public async Task OnGetAsync_ValidCultureEnGB_SetsCookieWithEnGB()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, httpContext) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync("en-GB", "/");

        // Assert
        var cookies = GetSetCookieHeaders(httpContext);
        Assert.Contains(cookies, c => c.Contains("en-GB"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Redirect validation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnGetAsync_NullRedirectUri_RedirectsToRoot()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, _) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        var result = await model.OnGetAsync("nl-BE", null!);

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    [Fact]
    public async Task OnGetAsync_EmptyRedirectUri_RedirectsToRoot()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, _) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        var result = await model.OnGetAsync("nl-BE", string.Empty);

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    [Fact]
    public async Task OnGetAsync_ValidLocalRedirectUri_RedirectsToUri()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, _) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        var result = await model.OnGetAsync("nl-BE", "/flights");

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/flights", redirect.Url);
    }

    [Fact]
    public async Task OnGetAsync_ExternalRedirectUri_RedirectsToRoot()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, _) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        var result = await model.OnGetAsync("nl-BE", "https://evil.com");

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Cookie attributes
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnGetAsync_AlwaysSetsExpectedCookieName()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, httpContext) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync("nl-BE", "/");

        // Assert — the cookie header must start with the expected cookie name
        var cookies = GetSetCookieHeaders(httpContext);
        Assert.Contains(cookies, c => c.StartsWith(CookieRequestCultureProvider.DefaultCookieName + "="));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UserManager persistence
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnGetAsync_UserLoggedIn_PersistsPreferredLocale()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var user = new ApplicationUser { UserName = "pilot@example.com", PreferredLocale = "nl-BE" };
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userMgr.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        var (model, _) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync("en-GB", "/");

        // Assert — UpdateAsync called once with the user whose locale was changed to en-GB
        userMgr.Verify(
            m => m.UpdateAsync(It.Is<ApplicationUser>(u => u.PreferredLocale == "en-GB")),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_UserNotLoggedIn_DoesNotCallUpdate()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, _) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        await model.OnGetAsync("en-GB", "/");

        // Assert
        userMgr.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task OnGetAsync_UpdateFails_LogsWarning_DoesNotThrow()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var user = new ApplicationUser { Id = "user-1", UserName = "pilot@example.com" };
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userMgr
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "DB error" }));

        var loggerMock = new Mock<ILogger<SetModel>>();
        var (model, _) = BuildSut(userMgr, loggerMock.Object);

        // Act — must not throw
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync("en-GB", "/"));

        // Assert — no exception, but a warning was logged
        Assert.Null(exception);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to persist PreferredLocale")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_UserNotLoggedIn_StillSetsCookieAndRedirects()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var (model, httpContext) = BuildSut(userMgr, NullLogger<SetModel>.Instance);

        // Act
        var result = await model.OnGetAsync("en-GB", "/dashboard");

        // Assert — cookie set and redirect valid
        var cookies = GetSetCookieHeaders(httpContext);
        Assert.Contains(cookies, c => c.Contains("en-GB"));
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ApplicationUser.PreferredLocale domain tests
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ApplicationUser_DefaultPreferredLocale_IsNlBE()
    {
        // Arrange / Act
        var user = new ApplicationUser();

        // Assert
        Assert.Equal("nl-BE", user.PreferredLocale);
    }

    [Fact]
    public void ApplicationUser_PreferredLocale_CanBeSetToEnGB()
    {
        // Arrange
        var user = new ApplicationUser();

        // Act
        user.PreferredLocale = "en-GB";

        // Assert
        Assert.Equal("en-GB", user.PreferredLocale);
    }

    [Fact]
    public void ApplicationUser_PreferredLocale_CanBeSetToNlBE()
    {
        // Arrange
        var user = new ApplicationUser { PreferredLocale = "en-GB" };

        // Act
        user.PreferredLocale = "nl-BE";

        // Assert
        Assert.Equal("nl-BE", user.PreferredLocale);
    }
}
