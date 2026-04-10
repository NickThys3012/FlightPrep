using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class PilotSettings : ComponentBase
{
    private List<Pilot> _pilots = [];
    private int? _editId;
    private Pilot _editPilot = new();
    private bool _addingNew;
    private Pilot _newPilot = new();
    private string? _userId;
    private bool _isAdmin;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _isAdmin = authState.User.IsInRole("Admin");
        await Load();
    }

    private async Task Load()
    {
        _pilots = await PilotSvc.GetAllAsync(_userId, _isAdmin);
    }

    private void StartEdit(Pilot p)
    {
        (_editId, _editPilot) = (p.Id, new Pilot { Id = p.Id, Name = p.Name, WeightKg = p.WeightKg });
    }

    private void CancelEdit()
    {
        _editId = null;
    }

    private async Task SaveEdit()
    {
        await PilotSvc.UpdateAsync(_editPilot, _userId, _isAdmin);
        _editId = null;
        await Load();
    }

    private async Task SaveNew()
    {
        await PilotSvc.AddAsync(_newPilot, _userId);
        _addingNew = false;
        await Load();
    }

    private async Task DeletePilot(int id)
    {
        await PilotSvc.DeleteAsync(id, _userId, _isAdmin);
        await Load();
    }
}
