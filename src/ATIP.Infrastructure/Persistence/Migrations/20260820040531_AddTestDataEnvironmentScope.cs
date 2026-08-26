using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestDataEnvironmentScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_test_data_sets_ProjectId_Name",
                table: "test_data_sets");

            migrationBuilder.AddColumn<Guid>(
                name: "EnvironmentId",
                table: "test_data_sets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_test_data_sets_EnvironmentId",
                table: "test_data_sets",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_test_data_sets_EnvironmentId_Name",
                table: "test_data_sets",
                columns: new[] { "EnvironmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_data_sets_ProjectId",
                table: "test_data_sets",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_test_data_sets_environments_EnvironmentId",
                table: "test_data_sets",
                column: "EnvironmentId",
                principalTable: "environments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_test_data_sets_environments_EnvironmentId",
                table: "test_data_sets");

            migrationBuilder.DropIndex(
                name: "IX_test_data_sets_EnvironmentId",
                table: "test_data_sets");

            migrationBuilder.DropIndex(
                name: "IX_test_data_sets_EnvironmentId_Name",
                table: "test_data_sets");

            migrationBuilder.DropIndex(
                name: "IX_test_data_sets_ProjectId",
                table: "test_data_sets");

            migrationBuilder.DropColumn(
                name: "EnvironmentId",
                table: "test_data_sets");

            migrationBuilder.CreateIndex(
                name: "IX_test_data_sets_ProjectId_Name",
                table: "test_data_sets",
                columns: new[] { "ProjectId", "Name" },
                unique: true);
        }
    }
}
