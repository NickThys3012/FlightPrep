using FlightPrep.Infrastructure.Data;
using FlightPrep.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
// Disambiguate: Microsoft.AspNetCore.Mvc also defines SignInResult.
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace FlightPrep.Tests;

/// <summary>
///     Unit tests for <see cref="LoginModel.OnPostAsync" />.
///     SignInManager and UserManager are mocked with Moq.
///     A real EF Core in-memory factory is provided for the fire-and-forget
///     <c>RecordLoginEvent</c> background task — any DB exceptions there are
///     silently caught by the method itself, so test assertions focus solely on
///     the return value and ModelState of OnPostAsync.
///     Note: RecordLoginEvent is private and runs as Task.Run (fire-and-forget).
///     Its DB persistence logic is tested independently in LoginEventTests
///     (FlightPrep.Infrastructure.Tests) using direct DB seeding and the same
///     query logic used in UserManagement.razor.
/// </summary>
public class LoginModelTests
{
    // ── Infrastructure helpers ────────────────────────────────────────────────

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManagerMock(
        Mock<UserManager<ApplicationUser>> userMgr)
        => new(
            userMgr.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);

    private static IDbContextFactory<AppDbContext> CreateDbFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    /// <summary>
    ///     Creates a fully wired <see cref="LoginModel" /> with a minimal PageContext
    ///     and an IUrlHelper mock whose <c>IsLocalUrl</c> returns true for paths
    ///     starting with "/" and false for everything else (null, absolute URLs, etc.).
    /// </summary>
    private static (LoginModel model, Mock<IUrlHelper> urlHelper)
        BuildSut(
            Mock<SignInManager<ApplicationUser>> signInMgr,
            Mock<UserManager<ApplicationUser>> userMgr)
    {
        var dbFactory = CreateDbFactory();
        var model = new LoginModel(
            signInMgr.Object,
            userMgr.Object,
            NullLogger<LoginModel>.Instance,
            dbFactory);

        // Minimal PageContext — required for ModelState and Url to work.
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new PageActionDescriptor(),
            modelState);
        model.PageContext = new PageContext(actionContext) { ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState) };

        // URL helper — local paths (starting with "/") are considered valid.
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper
            .Setup(u => u.IsLocalUrl(It.Is<string?>(s => s != null && s.StartsWith("/"))))
            .Returns(true);
        urlHelper
            .Setup(u => u.IsLocalUrl(It.Is<string?>(s => s == null || !s.StartsWith("/"))))
            .Returns(false);
        model.Url = urlHelper.Object;

        return (model, urlHelper);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_ValidApprovedUser_RedirectsToReturnUrl()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);

        signInMgr.Setup(s => s.PasswordSignInAsync(
                "pilot@example.com", "P@ss!", false, true))
            .ReturnsAsync(IdentitySignInResult.Success);

        userMgr.Setup(u => u.FindByEmailAsync("pilot@example.com"))
            .ReturnsAsync(new ApplicationUser { IsApproved = true });

        var (model, _) = BuildSut(signInMgr, userMgr);
        model.Input = new LoginModel.InputModel { Email = "pilot@example.com", Password = "P@ss!" };

        // Act
        var result = await model.OnPostAsync("/flights");

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/flights", redirect.Url);
    }

    [Fact]
    public async Task OnPostAsync_ValidApprovedUser_RedirectsToHomeWhenNoReturnUrl()
    {
        // Arrange — no returnUrl supplied; Url.IsLocalUrl(null) → false → defaults to "/"
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);

        signInMgr.Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Success);

        userMgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { IsApproved = true });

        var (model, _) = BuildSut(signInMgr, userMgr);
        model.Input = new LoginModel.InputModel { Email = "pilot@example.com", Password = "P@ss!" };

        // Act
        var result = await model.OnPostAsync();

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    [Fact]
    public async Task OnPostAsync_InvalidReturnUrl_RedirectsToHome()
    {
        // Arrange — absolute URL rejected by IsLocalUrl → falls back to "/"
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);

        signInMgr.Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Success);

        userMgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { IsApproved = true });

        var (model, _) = BuildSut(signInMgr, userMgr);
        model.Input = new LoginModel.InputModel { Email = "pilot@example.com", Password = "P@ss!" };

        // Act — supply an absolute (non-local) URL
        var result = await model.OnPostAsync("http://evil.example.com/steal");

        // Assert — sanitised home
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    // ── Unapproved user ───────────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_UnapprovedUser_SignsOutAndAddsModelError()
    {
        // Arrange — password corrects but an account not yet approved
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);

        signInMgr.Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Success);

        signInMgr.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);

        userMgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { IsApproved = false });

        var (model, _) = BuildSut(signInMgr, userMgr);
        model.Input = new LoginModel.InputModel { Email = "newpilot@example.com", Password = "P@ss!" };

        // Act
        var result = await model.OnPostAsync("/");

        // Assert — cookie revoked, page returned, error message added
        signInMgr.Verify(s => s.SignOutAsync(), Times.Once);
        Assert.IsType<PageResult>(result);

        var errors = model.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        Assert.Contains(errors, e => e.Contains("pending admin approval"));
    }

    // ── Locked-out user ───────────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_LockedOutUser_AddsLockoutModelError()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);

        signInMgr.Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.LockedOut);

        var (model, _) = BuildSut(signInMgr, userMgr);
        model.Input = new LoginModel.InputModel { Email = "locked@example.com", Password = "wrong" };

        // Act
        var result = await model.OnPostAsync();

        // Assert
        Assert.IsType<PageResult>(result);

        var errors = model.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        Assert.Contains(errors, e => e.Contains("vergrendeld") || e.ToLower().Contains("locked"));
    }

    // ── Invalid credentials ───────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_InvalidCredentials_AddsInvalidModelError()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);

        signInMgr.Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Failed);

        var (model, _) = BuildSut(signInMgr, userMgr);
        model.Input = new LoginModel.InputModel { Email = "pilot@example.com", Password = "wrongpassword" };

        // Act
        var result = await model.OnPostAsync();

        // Assert
        Assert.IsType<PageResult>(result);

        var errors = model.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        // The message is "Ongeldige inloggegevens." in Dutch
        Assert.NotEmpty(errors);
    }

    // ── Invalid model state ───────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        // Arrange — simulate model-binding failure (e.g. missing required Email)
        var userMgr = CreateUserManagerMock();
        var signInMgr = CreateSignInManagerMock(userMgr);
        var (model, _) = BuildSut(signInMgr, userMgr);

        model.ModelState.AddModelError("Input.Email", "E-mailadres is verplicht.");

        // Act
        var result = await model.OnPostAsync();

        // Assert — short-circuits before calling PasswordSignInAsync
        Assert.IsType<PageResult>(result);
        signInMgr.Verify(
            s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }
}
