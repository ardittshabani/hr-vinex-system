using Microsoft.EntityFrameworkCore;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Domain.Entities;

namespace Vinex.HR.Infrastructure.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options), IHrDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public const string IdSequence = "HrIdSequence";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Client-side key generation (HiLo) so ids exist before SaveChanges and audit rows
        // for new entities can be written in the same transaction as the change itself.
        // The sequence is named on every key explicitly: a model-wide UseHiLo(name) does not round-trip
        // through the migration snapshot and leaves the model permanently "changed".
        modelBuilder.HasSequence<int>(IdSequence).IncrementsBy(10);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var key = entity.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(int))
            {
                modelBuilder.Entity(entity.ClrType).Property(key.Properties[0].Name).UseHiLo(IdSequence);
            }
        }
    }
}
