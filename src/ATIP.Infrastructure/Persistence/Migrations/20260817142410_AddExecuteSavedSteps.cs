using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecuteSavedSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExecuteSavedSteps",
                table: "exploration_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecuteSavedSteps",
                table: "exploration_sessions");
        }
    }
}
