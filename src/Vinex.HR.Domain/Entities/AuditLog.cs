namespace Vinex.HR.Domain.Entities;

/// <summary>Append-only audit record. Never updated or deleted by the application.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int? UserId { get; set; }

    /// <summary>Stored as text (see <see cref="Enums.AuditAction"/>).</summary>
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    /// <summary>Short human-readable sentence; feeds the dashboard "recent activity".</summary>
    public string? Summary { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}
