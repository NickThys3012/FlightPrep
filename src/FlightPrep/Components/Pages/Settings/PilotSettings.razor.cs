using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class PilotSettings: ComponentBase
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
        await using var db = await DbFactory.CreateDbContextAsync();
        var query = db.Pilots.AsQueryable();
        if (!_isAdmin)
        {
            if (_userId == null) { _pilots = []; return; }
            query = query.Where(p => p.OwnerId == _userId);
        }
        _pilots = await query.OrderBy(p => p.Name).ToListAsync();
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
        await using var db = await DbFactory.CreateDbContextAsync();
        var p = await db.Pilots.FindAsync(_editPilot.Id);
        if (p is null || (!_isAdmin && p.OwnerId != _userId)) return;
        var originalOwnerId = p.OwnerId;
        db.Entry(p).CurrentValues.SetValues(_editPilot);
        p.OwnerId = originalOwnerId;
        await db.SaveChangesAsync();
        _editId = null;
        await Load();
    }

    private async Task SaveNew()
    {
        _newPilot.OwnerId = _userId;
        await using var db = await DbFactory.CreateDbContextAsync();
        db.Pilots.Add(_newPilot);
        await db.SaveChangesAsync();
        _addingNew = false;
        await Load();
    }

    private async Task DeletePilot(int id)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var p = await db.Pilots.FindAsync(id);
        if (p != null && (_isAdmin || p.OwnerId == _userId))
        {
            db.Pilots.Remove(p);
            await db.SaveChangesAsync();
        }
        await Load();
    }

}
