using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTemplate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// SwitchToOidcUsers added external_id as NOT NULL with DEFAULT '' (EF Core's way to fill
    /// existing rows), and the default stayed. The model has none, so an insert that forgot the
    /// column silently stored '' instead of failing. The model snapshot is unchanged: EF never
    /// knew about the default, which is why MigrationDriftTests compares real schemas.
    /// </summary>
    public partial class DropExternalIdDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN external_id DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN external_id SET DEFAULT '';");
        }
    }
}
