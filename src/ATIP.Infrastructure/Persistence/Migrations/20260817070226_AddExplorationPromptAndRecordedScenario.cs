using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExplorationPromptAndRecordedScenario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prompt",
                table: "exploration_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedScenarioId",
                table: "exploration_sessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prompt",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "RecordedScenarioId",
                table: "exploration_sessions");
        }
    }
}
