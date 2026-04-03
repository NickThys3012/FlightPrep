using FlightPrep.Data;
using FlightPrep.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FlightPrep.Tests;

/// <summary>
/// Unit tests for <see cref="AdminSeeder.SeedAdminAsync"/>.
/// UserManager and RoleManager are mocked with Moq; a real ServiceProvider
/// is built from ServiceCollection so that GetRequiredService&lt;T&gt; works
/// through the standard extension-method path.
/// </summary>
public class AdminSeederTests
{
    // ── Mock helpers ──────────────────────────────────────────────────────────

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        mgr.Object.Logger = NullLogger<UserManager<ApplicationUser>>.Instance;
        return mgr;
    }

    private static Mock<RoleManager<IdentityRole>> CreateRoleManagerMock()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object, null, null, null, null);
    }

    private static IConfiguration BuildConfig(string? username, string? password)
    {
        var data = new Dictionary<string, string?>();
        if (username != null) data["SEED_ADMIN_USERNAME"] = username;
        if (password != null) data["SEED_ADMIN_PASSWORD"] = password;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    /// <summary>
    /// Builds a real <see cref="IServiceProvider"/> that returns the provided
    /// mocked instances when <c>GetRequiredService&lt;T&gt;</c> is called.
    /// </summary>
    private static IServiceProvider BuildProvider(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config)
        => new ServiceCollection()
            .AddSingleton(userManager)
            .AddSingleton(roleManager)
            .AddSingleton(config)
            .BuildServiceProvider();

    // ── Early-exit: missing env-var ───────────────────────────────────────────

    [Fact]
    public async Task SeedAdminAsync_MissingUsername_DoesNotCreateUser()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        var config = BuildConfig(username: null, password: "P@ssw0rd!");
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        await AdminSeeder.SeedAdminAsync(sp);

        // Assert
        userMgr.Verify(
            u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAdminAsync_MissingPassword_DoesNotCreateUser()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        var config = BuildConfig(username: "admin@example.com", password: null);
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        await AdminSeeder.SeedAdminAsync(sp);

        // Assert
        userMgr.Verify(
            u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAdminAsync_ValidCredentials_CreatesAdminUserWithApprovedTrue()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        userMgr.Setup(u => u.FindByNameAsync("admin@example.com"))
               .ReturnsAsync((ApplicationUser?)null);
        userMgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), "P@ssw0rd!"))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
               .ReturnsAsync(IdentityResult.Success);
        var config = BuildConfig("admin@example.com", "P@ssw0rd!");
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        await AdminSeeder.SeedAdminAsync(sp);

        // Assert — user created with IsApproved = true and correct email
        userMgr.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(a =>
                a.IsApproved &&
                a.Email == "admin@example.com" &&
                a.EmailConfirmed),
            "P@ssw0rd!"), Times.Once);
    }

    [Fact]
    public async Task SeedAdminAsync_ValidCredentials_AddsUserToAdminRole()
    {
        // Arrange
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        userMgr.Setup(u => u.FindByNameAsync(It.IsAny<string>()))
               .ReturnsAsync((ApplicationUser?)null);
        userMgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
               .ReturnsAsync(IdentityResult.Success);
        var config = BuildConfig("admin@example.com", "P@ssw0rd!");
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        await AdminSeeder.SeedAdminAsync(sp);

        // Assert
        userMgr.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"), Times.Once);
    }

    [Fact]
    public async Task SeedAdminAsync_ValidCredentials_CreatesBothRoles()
    {
        // Arrange — neither role exists yet
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        roleMgr.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.FindByNameAsync(It.IsAny<string>()))
               .ReturnsAsync((ApplicationUser?)null);
        userMgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);
        var config = BuildConfig("admin@example.com", "P@ssw0rd!");
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        await AdminSeeder.SeedAdminAsync(sp);

        // Assert — exactly one CreateAsync call per role
        roleMgr.Verify(r => r.CreateAsync(It.Is<IdentityRole>(x => x.Name == "Admin")), Times.Once);
        roleMgr.Verify(r => r.CreateAsync(It.Is<IdentityRole>(x => x.Name == "Pilot")), Times.Once);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAdminAsync_UserAlreadyExists_DoesNotCreateDuplicate()
    {
        // Arrange
        var existing = new ApplicationUser
        {
            UserName = "admin@example.com",
            Email    = "admin@example.com",
            IsApproved = true
        };
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        userMgr.Setup(u => u.FindByNameAsync("admin@example.com")).ReturnsAsync(existing);
        var config = BuildConfig("admin@example.com", "P@ssw0rd!");
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        await AdminSeeder.SeedAdminAsync(sp);

        // Assert — no second user created
        userMgr.Verify(
            u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAdminAsync_RolesAlreadyExist_DoesNotThrow()
    {
        // Arrange — roles already exist; CreateAsync for roles must NOT be called
        var userMgr = CreateUserManagerMock();
        var roleMgr = CreateRoleManagerMock();
        roleMgr.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        userMgr.Setup(u => u.FindByNameAsync(It.IsAny<string>()))
               .ReturnsAsync((ApplicationUser?)null);
        userMgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);
        var config = BuildConfig("admin@example.com", "P@ssw0rd!");
        var sp = BuildProvider(userMgr.Object, roleMgr.Object, config);

        // Act
        var ex = await Record.ExceptionAsync(() => AdminSeeder.SeedAdminAsync(sp));

        // Assert — no exception, roles not re-created
        Assert.Null(ex);
        roleMgr.Verify(r => r.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
    }
}
