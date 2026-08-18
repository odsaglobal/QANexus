using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioStepRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "scenario_steps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecordedActionsJson",
                table: "scenario_steps",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "scenario_steps",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "scenario_steps");

            migrationBuilder.DropColumn(
                name: "RecordedActionsJson",
                table: "scenario_steps");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "scenario_steps");
        }
    }
}
