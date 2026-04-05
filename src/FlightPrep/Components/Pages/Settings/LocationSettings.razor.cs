using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
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
        await using var db = await DbFactory.CreateDbContextAsync();
        var query = db.Locations.AsQueryable();
        if (!_isAdmin)
        {
            if (_userId == null) { _locations = []; return; }
            query = query.Where(l => l.OwnerId == _userId);
        }
        _locations = await query.OrderBy(l => l.Name).ToListAsync();
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
        await using var db = await DbFactory.CreateDbContextAsync();
        var l = await db.Locations.FindAsync(_editLoc.Id);
        if (l is null || (!_isAdmin && l.OwnerId != _userId)) return;
        var originalOwnerId = l.OwnerId;
        db.Entry(l).CurrentValues.SetValues(_editLoc);
        l.OwnerId = originalOwnerId;
        await db.SaveChangesAsync();
        _editId = null;
        await Load();
    }

    private async Task SaveNew()
    {
        _newLoc.OwnerId = _userId;
        await using var db = await DbFactory.CreateDbContextAsync();
        db.Locations.Add(_newLoc);
        await db.SaveChangesAsync();
        _addingNew = false;
        await Load();
    }

    private async Task DeleteLocation(int id)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var l = await db.Locations.FindAsync(id);
        if (l != null && (_isAdmin || l.OwnerId == _userId))
        {
            db.Locations.Remove(l);
            await db.SaveChangesAsync();
        }

        await Load();
    }

}
