using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class LocationSettings : ComponentBase
{
    private List<Location> _locations = [];
    private int? _editId;
    private Location _editLoc = new();
    private bool _addingNew;
    private Location _newLoc = new();
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
        _locations = await LocationSvc.GetAllAsync(_userId, _isAdmin);
    }

    private void StartEdit(Location l)
    {
        (_editId, _editLoc) = (l.Id, new Location { Id = l.Id, Name = l.Name, IcaoCode = l.IcaoCode, AirspaceNotes = l.AirspaceNotes, Latitude = l.Latitude, Longitude = l.Longitude, ElevationM = l.ElevationM });
    }

    private void CancelEdit()
    {
        _editId = null;
    }

    private async Task SaveEdit()
    {
        await LocationSvc.UpdateAsync(_editLoc, _userId, _isAdmin);
        _editId = null;
        await Load();
    }

    private async Task SaveNew()
    {
        await LocationSvc.AddAsync(_newLoc, _userId);
        _addingNew = false;
        await Load();
    }

    private async Task DeleteLocation(int id)
    {
        await LocationSvc.DeleteAsync(id, _userId, _isAdmin);
        await Load();
    }
}
