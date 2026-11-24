using Vinex.HR.Domain.Common;

namespace Vinex.HR.Domain.Entities;

/// <summary>A rotating refresh token. Only a SHA-256 hash of the token is stored.</summary>
public class RefreshToken : Entity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
