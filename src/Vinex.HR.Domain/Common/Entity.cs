namespace Vinex.HR.Domain.Common;

/// <summary>Base class for all entities with an integer key.</summary>
public abstract class Entity
{
    public int Id { get; set; }
}

/// <summary>
/// Base class for mutable business entities: timestamps (UTC), who changed them,
/// and a row version for optimistic concurrency. Stamps are set by the persistence layer.
/// </summary>
public abstract class AuditableEntity : Entity
{
    [AuditIgnore] public DateTime CreatedAt { get; set; }
    [AuditIgnore] public DateTime UpdatedAt { get; set; }
    [AuditIgnore] public int? CreatedByUserId { get; set; }
    [AuditIgnore] public int? UpdatedByUserId { get; set; }
    [AuditIgnore] public byte[] RowVersion { get; set; } = [];
}

/// <summary>Excludes a property from the automatic audit trail (secrets, noisy counters, stamps).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute
{
}
