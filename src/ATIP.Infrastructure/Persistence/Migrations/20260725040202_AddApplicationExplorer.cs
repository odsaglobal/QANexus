using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationExplorer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exploration_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SeedUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MaxPages = table.Column<int>(type: "integer", nullable: false),
                    MaxDepth = table.Column<int>(type: "integer", nullable: false),
                    PagesDiscovered = table.Column<int>(type: "integer", nullable: false),
                    ElementsDiscovered = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exploration_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exploration_sessions_environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exploration_sessions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "discovered_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    AccessibilityTreeJson = table.Column<string>(type: "text", nullable: true),
                    DomSnapshotPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ScreenshotPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsExplored = table.Column<bool>(type: "boolean", nullable: false),
                    DepthFromRoot = table.Column<int>(type: "integer", nullable: false),
                    DiscoveredFromUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discovered_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_discovered_pages_exploration_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "exploration_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "discovered_elements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AriaLabel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TextContent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataTestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NearbyLabelsJson = table.Column<string>(type: "jsonb", nullable: true),
                    DomPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BoundingBoxJson = table.Column<string>(type: "jsonb", nullable: true),
                    ScreenshotPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AiDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsInteractive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ElementVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discovered_elements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_discovered_elements_discovered_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "discovered_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "element_locators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_element_locators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_element_locators_discovered_elements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "discovered_elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_discovered_elements_PageId",
                table: "discovered_elements",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_discovered_elements_ProjectId",
                table: "discovered_elements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_discovered_pages_ProjectId",
                table: "discovered_pages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_discovered_pages_SessionId",
                table: "discovered_pages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_element_locators_ElementId",
                table: "element_locators",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_EnvironmentId",
                table: "exploration_sessions",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_ProjectId",
                table: "exploration_sessions",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "element_locators");

            migrationBuilder.DropTable(
                name: "discovered_elements");

            migrationBuilder.DropTable(
                name: "discovered_pages");

            migrationBuilder.DropTable(
                name: "exploration_sessions");
        }
    }
}
