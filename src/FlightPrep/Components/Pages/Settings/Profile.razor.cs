using FlightPrep.Infrastructure.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class Profile : ComponentBase
{
    private ApplicationUser? _user;
    private bool _saved;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
            _user = await UserMgr.FindByIdAsync(userId);
    }

    private async Task Save()
    {
        if (_user == null) return;
        _error = null;
        var result = await UserMgr.UpdateAsync(_user);
        if (result.Succeeded)
        {
            _saved = true;
            StateHasChanged();
            await Task.Delay(2500);
            _saved = false;
        }
        else
        {
            _error = string.Join("; ", result.Errors.Select(e => e.Description));
        }
    }
}
