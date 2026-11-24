using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Vinex.HR.Domain.Entities;

namespace Vinex.HR.Application.Abstractions;

/// <summary>The slice of EF Core the application layer needs. Implemented by the Infrastructure DbContext.</summary>
public interface IHrDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemSetting> SystemSettings { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>The authenticated caller (null/false for anonymous or background work).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
}

public interface IPasswordService
{
    string Hash(string password);

    /// <summary>Verifies a password. <paramref name="needsRehash"/> is true when the stored hash uses outdated parameters.</summary>
    bool Verify(string hash, string password, out bool needsRehash);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user, string roleName);
}

/// <summary>Writes explicit audit events (logins, resets). Entity changes are audited automatically by the DbContext.</summary>
public interface IAuditLogger
{
    void Log(Vinex.HR.Domain.Enums.AuditAction action, string entityName, string? entityId, string summary,
        string? reason = null, int? userId = null);
}
