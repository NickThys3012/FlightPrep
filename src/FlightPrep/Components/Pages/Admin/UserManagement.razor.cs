using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Admin;

public partial class UserManagement : ComponentBase
{
    private List<UserViewModel>? _users;
    private List<LoginEvent>? _loginEvents;
    private string? _errorMessage;
    private string _activeTab = "users";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadUsers(), LoadLoginEvents());
    }

    private async Task LoadUsers()
    {
        _errorMessage = null;
        var users = UserManager.Users.ToList();
        var vms = new List<UserViewModel>();
        foreach (var u in users)
        {
            var roles = await UserManager.GetRolesAsync(u);
            vms.Add(new UserViewModel { Id = u.Id, Email = u.Email!, IsApproved = u.IsApproved, Roles = roles.ToList() });
        }

        _users = vms;
    }

    private async Task LoadLoginEvents()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        _loginEvents = await db.LoginEvents
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .ToListAsync();
    }

    private async Task ApproveUser(string id)
    {
        var user = await UserManager.FindByIdAsync(id);
        if (user != null)
        {
            user.IsApproved = true;
            await UserManager.UpdateAsync(user);
            Logger.LogInformation("Admin approved user {UserId}", id);
            await LoadUsers();
        }
    }

    private async Task DeleteUser(string id)
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (id == currentUserId)
        {
            _errorMessage = "You cannot delete your own account.";
            return;
        }

        var user = await UserManager.FindByIdAsync(id);
        if (user != null)
        {
            await UserManager.DeleteAsync(user);
            Logger.LogInformation("Admin deleted user {UserId}", id);
            await LoadUsers();
        }
    }

    private class UserViewModel
    {
        public string Id { get; init; } = "";
        public string Email { get; init; } = "";
        public bool IsApproved { get; init; }
        public List<string> Roles { get; init; } = [];
    }

}
