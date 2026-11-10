using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExplorationSessionQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnsweredQuestionsJson",
                table: "exploration_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingQuestion",
                table: "exploration_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingQuestionAnswer",
                table: "exploration_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingQuestionAskedAtUtc",
                table: "exploration_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingQuestionContext",
                table: "exploration_sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnsweredQuestionsJson",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "PendingQuestion",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "PendingQuestionAnswer",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "PendingQuestionAskedAtUtc",
                table: "exploration_sessions");

            migrationBuilder.DropColumn(
                name: "PendingQuestionContext",
                table: "exploration_sessions");
        }
    }
}
