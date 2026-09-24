using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTemplate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToOidcUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "password", table: "users");

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: ""
            );

            // Rows created before OIDC (seed fixtures, password-based accounts) have no Keycloak
            // identity - give each a unique placeholder so the unique index below can be built.
            migrationBuilder.Sql("UPDATE users SET external_id = 'legacy-' || id;");

            migrationBuilder.CreateIndex(
                name: "ix_users_external_id",
                table: "users",
                column: "external_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_users_external_id", table: "users");

            migrationBuilder.DropColumn(name: "external_id", table: "users");

            migrationBuilder.AddColumn<string>(
                name: "password",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: ""
            );
        }
    }
}
