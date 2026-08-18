using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestSuites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SuiteId",
                table: "exploration_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "test_suites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_suites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_test_suites_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "test_suite_scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_suite_scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_test_suite_scenarios_scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_test_suite_scenarios_test_suites_SuiteId",
                        column: x => x.SuiteId,
                        principalTable: "test_suites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_SuiteId",
                table: "exploration_sessions",
                column: "SuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_test_suite_scenarios_ScenarioId",
                table: "test_suite_scenarios",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_test_suite_scenarios_SuiteId_ScenarioId",
                table: "test_suite_scenarios",
                columns: new[] { "SuiteId", "ScenarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_suites_ProjectId",
                table: "test_suites",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "test_suite_scenarios");

            migrationBuilder.DropTable(
                name: "test_suites");

            migrationBuilder.DropIndex(
                name: "IX_exploration_sessions_SuiteId",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "SuiteId",
                table: "exploration_sessions");
        }
    }
}
