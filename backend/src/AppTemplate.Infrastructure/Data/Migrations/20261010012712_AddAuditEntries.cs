using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTemplate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    actor = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    action = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    entity_type = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    entity_key = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    outcome = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    trace_id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_actor_occurred_at_utc",
                table: "audit_entries",
                columns: new[] { "actor", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_entity_type_entity_key_occurred_at_utc",
                table: "audit_entries",
                columns: new[] { "entity_type", "entity_key", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_occurred_at_utc",
                table: "audit_entries",
                column: "occurred_at_utc"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "audit_entries");
        }
    }
}
