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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Client-side key generation (HiLo) so ids exist before SaveChanges and audit rows
        // for new entities can be written in the same transaction as the change itself.
        modelBuilder.UseHiLo("HrIdSequence");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrDbContext).Assembly);
    }
}
