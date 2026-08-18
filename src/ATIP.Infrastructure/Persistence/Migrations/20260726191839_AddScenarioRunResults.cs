using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioRunResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioId",
                table: "exploration_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "scenario_step_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_step_results", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_ScenarioId",
                table: "exploration_sessions",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_step_results_ScenarioId",
                table: "scenario_step_results",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_step_results_SessionId",
                table: "scenario_step_results",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scenario_step_results");

            migrationBuilder.DropIndex(
                name: "IX_exploration_sessions_ScenarioId",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "ScenarioId",
                table: "exploration_sessions");
        }
    }
}
