using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnalyzedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_requirements_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requirement_modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requirement_modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_requirement_modules_requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BusinessRulesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_features_requirement_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "requirement_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Risk = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Preconditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpectedResult = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scenarios_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_stories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsA = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IWant = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SoThat = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcceptanceCriteriaJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_stories_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExpectedResult = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scenario_steps_scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_features_ModuleId",
                table: "features",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_features_ProjectId",
                table: "features",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_requirement_modules_ProjectId",
                table: "requirement_modules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_requirement_modules_RequirementId",
                table: "requirement_modules",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_requirements_ProjectId",
                table: "requirements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_steps_ScenarioId",
                table: "scenario_steps",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_FeatureId",
                table: "scenarios",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_ProjectId",
                table: "scenarios",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_user_stories_FeatureId",
                table: "user_stories",
                column: "FeatureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scenario_steps");

            migrationBuilder.DropTable(
                name: "user_stories");

            migrationBuilder.DropTable(
                name: "scenarios");

            migrationBuilder.DropTable(
                name: "features");

            migrationBuilder.DropTable(
                name: "requirement_modules");

            migrationBuilder.DropTable(
                name: "requirements");
        }
    }
}
