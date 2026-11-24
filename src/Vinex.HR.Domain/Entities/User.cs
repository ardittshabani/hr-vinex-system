using Vinex.HR.Domain.Common;

namespace Vinex.HR.Domain.Entities;

public class User : AuditableEntity
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }

    [AuditIgnore] public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }

    [AuditIgnore] public int FailedLoginCount { get; set; }
    [AuditIgnore] public DateTime? LockoutEndAt { get; set; }
    [AuditIgnore] public DateTime? LastLoginAt { get; set; }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndAt.HasValue && LockoutEndAt.Value > utcNow;
}
