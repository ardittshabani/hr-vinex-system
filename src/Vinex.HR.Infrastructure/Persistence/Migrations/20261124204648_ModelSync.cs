using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinex.HR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "EntityFrameworkHiLoSequence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "EntityFrameworkHiLoSequence",
                incrementBy: 10);
        }
    }
}
