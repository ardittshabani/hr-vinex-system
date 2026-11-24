using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Domain.Entities;
using Vinex.HR.Infrastructure.Persistence;

namespace Vinex.HR.Infrastructure.Seeding;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations (or, only when explicitly allowed and no migrations exist yet, creates the schema),
    /// then seeds reference data and the bootstrap administrator.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var config = sp.GetRequiredService<IConfiguration>();
        var db = sp.GetRequiredService<HrDbContext>();

        if (config.GetValue("Database:Migrate", true))
        {
            await WaitForDatabaseAsync(db, logger, ct);

            if (db.Database.GetMigrations().Any())
            {
                logger.LogInformation("Applying database migrations...");
                await db.Database.MigrateAsync(ct);
            }
            else if (config.GetValue("Database:AllowEnsureCreated", false))
            {
                logger.LogWarning("No migrations found; creating schema with EnsureCreated (Database:AllowEnsureCreated=true). " +
                                  "Generate the InitialCreate migration before production use.");
                await db.Database.EnsureCreatedAsync(ct);
            }
            else
            {
                throw new InvalidOperationException(
                    "No EF migrations exist. Run the 'add-migration' workflow / `dotnet ef migrations add InitialCreate`, " +
                    "or set Database:AllowEnsureCreated=true for a throwaway environment.");
            }
        }

        await SeedAsync(sp, config, logger, ct);
    }

    private static async Task WaitForDatabaseAsync(HrDbContext db, ILogger logger, CancellationToken ct)
    {
        const int attempts = 20;
        for (var i = 1; i <= attempts; i++)
        {
            try
            {
                if (await db.Database.CanConnectAsync(ct) || await TryServerReachableAsync(db, ct))
                {
                    return;
                }
            }
            catch (Exception ex) when (i < attempts)
            {
                logger.LogWarning("Database not reachable yet ({Attempt}/{Max}): {Message}", i, attempts, ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        throw new InvalidOperationException("The SQL Server database could not be reached.");
    }

    // CanConnect returns false when the server is up but the database does not exist yet; that is fine before migrating.
    private static async Task<bool> TryServerReachableAsync(HrDbContext db, CancellationToken ct)
    {
        try
        {
            var cs = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.Database.GetConnectionString())
            {
                InitialCatalog = "master",
            };
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs.ConnectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task SeedAsync(IServiceProvider sp, IConfiguration config, ILogger logger, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HrDbContext>();

        foreach (var roleName in RoleNames.All)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == roleName, ct))
            {
                db.Roles.Add(new Role { Name = roleName });
            }
        }

        var departments = new Dictionary<string, string[]>
        {
            ["Production"] = ["Machine Operator", "Production Supervisor", "Quality Inspector"],
            ["Warehouse"] = ["Warehouse Clerk", "Forklift Operator"],
            ["Maintenance"] = ["Maintenance Technician", "IT / Systems Engineer"],
            ["Administration"] = ["HR Officer", "Accountant", "Office Manager"],
        };

        foreach (var (deptName, positions) in departments)
        {
            var dept = await db.Departments.FirstOrDefaultAsync(d => d.Name == deptName, ct);
            if (dept is null)
            {
                dept = new Department { Name = deptName };
                db.Departments.Add(dept);
                foreach (var title in positions)
                {
                    dept.Positions.Add(new Position { Title = title });
                }
            }
        }

        var defaults = new Dictionary<string, string>
        {
            ["Company.Name"] = "Vinex Fasteners",
            ["Company.TimeZone"] = "Europe/Belgrade",
            ["Security.IdleTimeoutMinutes"] = "30",
        };
        foreach (var (key, value) in defaults)
        {
            if (!await db.SystemSettings.AnyAsync(s => s.Key == key, ct))
            {
                db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
            }
        }

        await db.SaveChangesAsync(ct);

        // Bootstrap administrator: only when no user exists, from configuration/environment (never hard-coded).
        if (!await db.Users.AnyAsync(ct))
        {
            var username = config["Bootstrap:AdminUsername"];
            var password = config["Bootstrap:AdminPassword"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No users exist and Bootstrap:AdminUsername/AdminPassword are not set; nobody can sign in yet.");
                return;
            }

            var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.HRAdministrator, ct);
            var passwords = sp.GetRequiredService<IPasswordService>();
            db.Users.Add(new User
            {
                Username = username.Trim(),
                DisplayName = config["Bootstrap:AdminDisplayName"] ?? "HR Administrator",
                RoleId = role.Id,
                PasswordHash = passwords.Hash(password),
                MustChangePassword = true,
                IsActive = true,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Bootstrap administrator '{Username}' created (password change required at first login).", username);
        }
    }
}
