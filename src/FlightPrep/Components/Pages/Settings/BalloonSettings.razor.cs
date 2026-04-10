using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace FlightPrep.Components.Pages.Settings;

public partial class BalloonSettings : ComponentBase
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
        _balloons = await BalloonSvc.GetAllAsync(_userId, _isAdmin);
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
        await BalloonSvc.UpdateAsync(_editBalloon, _userId, _isAdmin);
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
        await BalloonSvc.AddAsync(_newBalloon, _userId);
        _addingNew = false;
        await Load();
    }

    private async Task DeleteBalloon(int id)
    {
        await BalloonSvc.DeleteAsync(id, _userId, _isAdmin);
        await Load();
    }
}
