using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnershipForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_user_stories_ProjectId",
                table: "user_stories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_step_results_ProjectId",
                table: "scenario_step_results",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_api_keys_tenants_TenantId",
                table: "api_keys",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_browser_profiles_tenants_TenantId",
                table: "browser_profiles",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_credentials_tenants_TenantId",
                table: "credentials",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_discovered_elements_projects_ProjectId",
                table: "discovered_elements",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_discovered_elements_tenants_TenantId",
                table: "discovered_elements",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_discovered_pages_projects_ProjectId",
                table: "discovered_pages",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_discovered_pages_tenants_TenantId",
                table: "discovered_pages",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_element_locators_tenants_TenantId",
                table: "element_locators",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_environments_tenants_TenantId",
                table: "environments",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exploration_sessions_tenants_TenantId",
                table: "exploration_sessions",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_features_projects_ProjectId",
                table: "features",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_features_tenants_TenantId",
                table: "features",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_tenants_TenantId",
                table: "notifications",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_project_members_tenants_TenantId",
                table: "project_members",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_requirement_modules_projects_ProjectId",
                table: "requirement_modules",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_requirement_modules_tenants_TenantId",
                table: "requirement_modules",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_requirements_tenants_TenantId",
                table: "requirements",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scenario_step_results_projects_ProjectId",
                table: "scenario_step_results",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scenario_step_results_tenants_TenantId",
                table: "scenario_step_results",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scenario_steps_tenants_TenantId",
                table: "scenario_steps",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scenarios_projects_ProjectId",
                table: "scenarios",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scenarios_tenants_TenantId",
                table: "scenarios",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_data_sets_tenants_TenantId",
                table: "test_data_sets",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_suite_scenarios_tenants_TenantId",
                table: "test_suite_scenarios",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_suites_tenants_TenantId",
                table: "test_suites",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_stories_projects_ProjectId",
                table: "user_stories",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_stories_tenants_TenantId",
                table: "user_stories",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_api_keys_tenants_TenantId",
                table: "api_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_browser_profiles_tenants_TenantId",
                table: "browser_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_credentials_tenants_TenantId",
                table: "credentials");

            migrationBuilder.DropForeignKey(
                name: "FK_discovered_elements_projects_ProjectId",
                table: "discovered_elements");

            migrationBuilder.DropForeignKey(
                name: "FK_discovered_elements_tenants_TenantId",
                table: "discovered_elements");

            migrationBuilder.DropForeignKey(
                name: "FK_discovered_pages_projects_ProjectId",
                table: "discovered_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_discovered_pages_tenants_TenantId",
                table: "discovered_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_element_locators_tenants_TenantId",
                table: "element_locators");

            migrationBuilder.DropForeignKey(
                name: "FK_environments_tenants_TenantId",
                table: "environments");

            migrationBuilder.DropForeignKey(
                name: "FK_exploration_sessions_tenants_TenantId",
                table: "exploration_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_features_projects_ProjectId",
                table: "features");

            migrationBuilder.DropForeignKey(
                name: "FK_features_tenants_TenantId",
                table: "features");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_tenants_TenantId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_project_members_tenants_TenantId",
                table: "project_members");

            migrationBuilder.DropForeignKey(
                name: "FK_requirement_modules_projects_ProjectId",
                table: "requirement_modules");

            migrationBuilder.DropForeignKey(
                name: "FK_requirement_modules_tenants_TenantId",
                table: "requirement_modules");

            migrationBuilder.DropForeignKey(
                name: "FK_requirements_tenants_TenantId",
                table: "requirements");

            migrationBuilder.DropForeignKey(
                name: "FK_scenario_step_results_projects_ProjectId",
                table: "scenario_step_results");

            migrationBuilder.DropForeignKey(
                name: "FK_scenario_step_results_tenants_TenantId",
                table: "scenario_step_results");

            migrationBuilder.DropForeignKey(
                name: "FK_scenario_steps_tenants_TenantId",
                table: "scenario_steps");

            migrationBuilder.DropForeignKey(
                name: "FK_scenarios_projects_ProjectId",
                table: "scenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_scenarios_tenants_TenantId",
                table: "scenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_test_data_sets_tenants_TenantId",
                table: "test_data_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_test_suite_scenarios_tenants_TenantId",
                table: "test_suite_scenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_test_suites_tenants_TenantId",
                table: "test_suites");

            migrationBuilder.DropForeignKey(
                name: "FK_user_stories_projects_ProjectId",
                table: "user_stories");

            migrationBuilder.DropForeignKey(
                name: "FK_user_stories_tenants_TenantId",
                table: "user_stories");

            migrationBuilder.DropIndex(
                name: "IX_user_stories_ProjectId",
                table: "user_stories");

            migrationBuilder.DropIndex(
                name: "IX_scenario_step_results_ProjectId",
                table: "scenario_step_results");
        }
    }
}
