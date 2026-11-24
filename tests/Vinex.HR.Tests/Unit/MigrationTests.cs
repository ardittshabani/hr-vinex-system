using Microsoft.EntityFrameworkCore;
using Vinex.HR.Infrastructure.Persistence;

namespace Vinex.HR.Tests.Unit;

public class MigrationTests
{
    [Fact]
    public void Model_has_no_changes_missing_from_migrations()
    {
        // No connection is opened: this compares the model with the latest migration snapshot.
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseSqlServer("Server=unused;Database=unused;TrustServerCertificate=True")
            .Options;
        using var db = new HrDbContext(options);
        db.Database.HasPendingModelChanges().ShouldBeFalse("Run the 'Add EF migration' workflow.");
    }
}
