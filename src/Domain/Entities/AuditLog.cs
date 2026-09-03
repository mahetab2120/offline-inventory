using Domain.Enums;

namespace Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Username { get; set; } = "SYSTEM";
    public string MachineName { get; set; } = Environment.MachineName;
    public string Module { get; set; } = string.Empty;
    public AuditActionType Action { get; set; }
    public string RecordId { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
}
