using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_user_stories_TenantId",
                table: "user_stories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_test_suites_TenantId",
                table: "test_suites",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_test_suite_scenarios_TenantId",
                table: "test_suite_scenarios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_test_data_sets_TenantId",
                table: "test_data_sets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_TenantId",
                table: "scenarios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_steps_TenantId",
                table: "scenario_steps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_step_results_TenantId",
                table: "scenario_step_results",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_requirements_TenantId",
                table: "requirements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_requirement_modules_TenantId",
                table: "requirement_modules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId",
                table: "projects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_TenantId",
                table: "project_members",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TenantId",
                table: "notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_features_TenantId",
                table: "features",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_TenantId",
                table: "exploration_sessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_environments_TenantId",
                table: "environments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_element_locators_TenantId",
                table: "element_locators",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_discovered_pages_TenantId",
                table: "discovered_pages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_discovered_elements_TenantId",
                table: "discovered_elements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_credentials_TenantId",
                table: "credentials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_browser_profiles_TenantId",
                table: "browser_profiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TenantId",
                table: "audit_logs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_stories_TenantId",
                table: "user_stories");

            migrationBuilder.DropIndex(
                name: "IX_test_suites_TenantId",
                table: "test_suites");

            migrationBuilder.DropIndex(
                name: "IX_test_suite_scenarios_TenantId",
                table: "test_suite_scenarios");

            migrationBuilder.DropIndex(
                name: "IX_test_data_sets_TenantId",
                table: "test_data_sets");

            migrationBuilder.DropIndex(
                name: "IX_scenarios_TenantId",
                table: "scenarios");

            migrationBuilder.DropIndex(
                name: "IX_scenario_steps_TenantId",
                table: "scenario_steps");

            migrationBuilder.DropIndex(
                name: "IX_scenario_step_results_TenantId",
                table: "scenario_step_results");

            migrationBuilder.DropIndex(
                name: "IX_requirements_TenantId",
                table: "requirements");

            migrationBuilder.DropIndex(
                name: "IX_requirement_modules_TenantId",
                table: "requirement_modules");

            migrationBuilder.DropIndex(
                name: "IX_projects_TenantId",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_project_members_TenantId",
                table: "project_members");

            migrationBuilder.DropIndex(
                name: "IX_notifications_TenantId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_features_TenantId",
                table: "features");

            migrationBuilder.DropIndex(
                name: "IX_exploration_sessions_TenantId",
                table: "exploration_sessions");

            migrationBuilder.DropIndex(
                name: "IX_environments_TenantId",
                table: "environments");

            migrationBuilder.DropIndex(
                name: "IX_element_locators_TenantId",
                table: "element_locators");

            migrationBuilder.DropIndex(
                name: "IX_discovered_pages_TenantId",
                table: "discovered_pages");

            migrationBuilder.DropIndex(
                name: "IX_discovered_elements_TenantId",
                table: "discovered_elements");

            migrationBuilder.DropIndex(
                name: "IX_credentials_TenantId",
                table: "credentials");

            migrationBuilder.DropIndex(
                name: "IX_browser_profiles_TenantId",
                table: "browser_profiles");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_TenantId",
                table: "audit_logs");
        }
    }
}
