using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Domain.Common;
using Vinex.HR.Domain.Entities;
using Vinex.HR.Domain.Enums;

namespace Vinex.HR.Infrastructure.Persistence;

/// <summary>
/// Stamps CreatedAt/UpdatedAt (+ user ids) and writes an AuditLog row for every create/update/delete of an
/// <see cref="AuditableEntity"/>, inside the same transaction as the change. Ids are HiLo-generated, so they
/// are already known for new entities.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, IClock clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entity = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
                entity.UpdatedAt = now;
                entity.CreatedByUserId = userId;
                entity.UpdatedByUserId = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entity.UpdatedAt = now;
                entity.UpdatedByUserId = userId;
            }

            var log = BuildLog(entry, now, userId);
            if (log is not null)
            {
                logs.Add(log);
            }
        }

        if (logs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(logs);
        }
    }

    private AuditLog? BuildLog(EntityEntry entry, DateTime now, int? userId)
    {
        var entityName = entry.Metadata.ClrType.Name;
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            if (prop.Metadata.IsPrimaryKey() || IsAuditIgnored(entry.Metadata.ClrType, prop.Metadata.Name))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[prop.Metadata.Name] = prop.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[prop.Metadata.Name] = prop.OriginalValue;
                    break;
                case EntityState.Modified when prop.IsModified:
                    oldValues[prop.Metadata.Name] = prop.OriginalValue;
                    newValues[prop.Metadata.Name] = prop.CurrentValue;
                    break;
            }
        }

        if (entry.State == EntityState.Modified && newValues.Count == 0)
        {
            return null; // only ignored/noisy properties changed
        }

        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Create,
            EntityState.Deleted => AuditAction.Delete,
            _ => AuditAction.Update,
        };

        var id = entry.Property("Id").CurrentValue?.ToString();
        var verb = action switch { AuditAction.Create => "Created", AuditAction.Delete => "Deleted", _ => "Updated" };
        var label = DisplayName(entry) ?? $"#{id}";

        return new AuditLog
        {
            Timestamp = now,
            UserId = userId,
            Action = action.ToString(),
            EntityName = entityName,
            EntityId = id,
            Summary = $"{verb} {entityName.ToLowerInvariant()} {label}",
            OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues, Json),
            NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues, Json),
            IpAddress = currentUser.IpAddress,
        };
    }

    private static string? DisplayName(EntityEntry entry)
    {
        foreach (var name in new[] { "Username", "Name", "Title" })
        {
            var prop = entry.Metadata.FindProperty(name);
            if (prop is not null && entry.Property(name).CurrentValue is string s && s.Length > 0)
            {
                return $"'{s}'";
            }
        }

        return null;
    }

    private static bool IsAuditIgnored(Type clrType, string propertyName) =>
        clrType.GetProperty(propertyName)?.GetCustomAttribute<AuditIgnoreAttribute>() is not null;
}
