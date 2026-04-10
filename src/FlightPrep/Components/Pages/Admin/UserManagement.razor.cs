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
        var users = await UserManager.Users.ToListAsync();

        // Batch-load all user-role mappings in a single query — eliminates N+1 GetRolesAsync calls.
        await using var db = await DbFactory.CreateDbContextAsync();
        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = await db.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, Name = r.Name ?? "" })
            .ToListAsync();
        var rolesByUserId = userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => ur.Name).ToList());

        _users = users.Select(u => new UserViewModel
        {
            Id = u.Id,
            Email = u.Email!,
            IsApproved = u.IsApproved,
            Roles = rolesByUserId.GetValueOrDefault(u.Id, [])
        }).ToList();
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
