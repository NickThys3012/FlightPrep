using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class OFPSettingsPage : ComponentBase
{
    private OFPSettings? _settings;
    private bool _saved;
    private string? _userId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _settings = await OFPSettingsSvc.GetSettingsAsync(_userId);
    }

    private async Task Save()
    {
        if (_settings == null || _userId == null) return;
        await OFPSettingsSvc.SaveSettingsAsync(_settings, _userId);
        _saved = true;
        StateHasChanged();
        await Task.Delay(2500);
        _saved = false;
    }

    private void Reset()
    {
        _settings = new OFPSettings();
        _saved = false;
    }
}
