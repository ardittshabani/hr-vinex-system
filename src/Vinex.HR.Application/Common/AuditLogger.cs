using Vinex.HR.Application.Abstractions;
using Vinex.HR.Domain.Entities;
using Vinex.HR.Domain.Enums;

namespace Vinex.HR.Application.Common;

internal sealed class AuditLogger(IHrDbContext db, IClock clock, ICurrentUser currentUser) : IAuditLogger
{
    public void Log(AuditAction action, string entityName, string? entityId, string summary,
        string? reason = null, int? userId = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = clock.UtcNow,
            UserId = userId ?? currentUser.UserId,
            Action = action.ToString(),
            EntityName = entityName,
            EntityId = entityId,
            Summary = Truncate(summary, 500),
            Reason = reason,
            IpAddress = currentUser.IpAddress,
        });
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
