using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppTemplate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AddColumn<bool>(
                name: "email_confirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "external_login",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    provider_key = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    linked_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_login", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_login_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    expires_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    revoked_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    replaced_by_token_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "user_token",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    purpose = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    token_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    expires_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    consumed_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_token_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_external_login_provider_provider_key",
                table: "external_login",
                columns: new[] { "provider", "provider_key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_external_login_user_id",
                table: "external_login",
                column: "user_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_token_hash",
                table: "refresh_token",
                column: "token_hash"
            );

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_user_id",
                table: "refresh_token",
                column: "user_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_user_token_token_hash",
                table: "user_token",
                column: "token_hash"
            );

            migrationBuilder.CreateIndex(
                name: "ix_user_token_user_id",
                table: "user_token",
                column: "user_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "external_login");

            migrationBuilder.DropTable(name: "refresh_token");

            migrationBuilder.DropTable(name: "user_token");

            migrationBuilder.DropColumn(name: "email_confirmed", table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "password",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );
        }
    }
}
