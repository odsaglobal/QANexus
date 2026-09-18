using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioAutoHeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing scenarios have no recorded scripts yet, so they must keep auto-heal ON —
            // otherwise their first deterministic run would fail every step outright. This matches
            // the entity default (Scenario.AutoHealEnabled = true).
            migrationBuilder.AddColumn<bool>(
                name: "AutoHealEnabled",
                table: "scenarios",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoHealEnabled",
                table: "scenarios");
        }
    }
}
