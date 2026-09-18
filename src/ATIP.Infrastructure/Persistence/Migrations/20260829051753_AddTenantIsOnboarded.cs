using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATIP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIsOnboarded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Existing tenants are already established → treat them as onboarded, EXCEPT ones whose
            // name was derived from the synthetic "users.noreply" placeholder domain, which should
            // be re-prompted to pick a real workspace name.
            migrationBuilder.Sql(
                "UPDATE tenants SET \"IsOnboarded\" = true " +
                "WHERE COALESCE(\"ExternalDirectoryId\", '') <> 'users.noreply';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "tenants");
        }
    }
}
