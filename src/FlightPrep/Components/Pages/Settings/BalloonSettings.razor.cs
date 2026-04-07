using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class BalloonSettings: ComponentBase
{
        private List<Balloon> _balloons = [];
    private int? _editId;
    private Balloon _editBalloon = new();
    private bool _addingNew;
    private Balloon _newBalloon = new();
    private string? _editError;
    private string? _newError;
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
        var query = db.Balloons.AsQueryable();
        if (!_isAdmin)
        {
            if (_userId == null) { _balloons = []; return; }
            query = query.Where(b => b.OwnerId == _userId);
        }
        _balloons = await query.OrderBy(b => b.Registration).ToListAsync();
    }

    private void StartEdit(Balloon b)
    {
        _editId = b.Id;
        _editBalloon = new Balloon
        {
            Id = b.Id,
            Registration = b.Registration,
            Type = b.Type,
            VolumeM3 = b.VolumeM3,
            InternalEnvelopeTempC = b.InternalEnvelopeTempC,
            EnvelopeOnlyWeightKg = b.EnvelopeOnlyWeightKg,
            BasketWeightKg = b.BasketWeightKg,
            BurnerWeightKg = b.BurnerWeightKg,
            CylindersWeightKg = b.CylindersWeightKg
        };
    }

    private void CancelEdit()
    {
        _editId = null;
    }

    private async Task SaveEdit()
    {
        if (_editBalloon.InternalEnvelopeTempC is < 0 or > 100)
        {
            _editError = "Inwendige temperatuur (Ti) moet tussen 0 en 100°C liggen.";
            return;
        }

        _editError = null;
        await using var db = await DbFactory.CreateDbContextAsync();
        var b = await db.Balloons.FindAsync(_editBalloon.Id);
        if (b is null || (!_isAdmin && b.OwnerId != _userId)) return;
        var originalOwnerId = b.OwnerId;
        db.Entry(b).CurrentValues.SetValues(_editBalloon);
        b.OwnerId = originalOwnerId;
        await db.SaveChangesAsync();
        _editId = null;
        await Load();
    }

    private async Task SaveNew()
    {
        if (_newBalloon.InternalEnvelopeTempC is < 0 or > 100)
        {
            _newError = "Inwendige temperatuur (Ti) moet tussen 0 en 100°C liggen.";
            return;
        }

        _newError = null;
        _newBalloon.OwnerId = _userId;
        await using var db = await DbFactory.CreateDbContextAsync();
        db.Balloons.Add(_newBalloon);
        await db.SaveChangesAsync();
        _addingNew = false;
        await Load();
    }

    private async Task DeleteBalloon(int id)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var b = await db.Balloons.FindAsync(id);
        if (b != null && (_isAdmin || b.OwnerId == _userId))
        {
            db.Balloons.Remove(b);
            await db.SaveChangesAsync();
        }

        await Load();
    }

}
