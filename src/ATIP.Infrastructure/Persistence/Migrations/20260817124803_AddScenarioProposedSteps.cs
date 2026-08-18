using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioProposedSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousStepsJson",
                table: "scenarios",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedStepsJson",
                table: "scenarios",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousStepsJson",
                table: "scenarios");

            migrationBuilder.DropColumn(
                name: "ProposedStepsJson",
                table: "scenarios");
        }
    }
}
