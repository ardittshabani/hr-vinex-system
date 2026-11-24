using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vinex.HR.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` so migrations can be created without running the API or a database.
/// </summary>
public sealed class HrDbContextFactory : IDesignTimeDbContextFactory<HrDbContext>
{
    public HrDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("HR_DESIGN_CONNECTION")
                         ?? "Server=localhost,1433;Database=VinexHR_Design;User Id=sa;Password=design-time-only;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<HrDbContext>().UseSqlServer(connection).Options;
        return new HrDbContext(options);
    }
}
