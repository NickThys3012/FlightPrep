namespace FlightPrep.Domain.Models;

public class LoginEvent
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? IpAddress { get; set; }
    public string? FailureReason { get; set; } // "InvalidPassword", "NotApproved", "LockedOut"
}
