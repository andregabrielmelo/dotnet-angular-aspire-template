using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTemplate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Model snapshot only: User.Version maps to Postgres's system column xmin, which every row
    /// already has. EF scaffolds an AddColumn for it, which Postgres would reject ("column name
    /// xmin conflicts with a system column"), so Up and Down are deliberately empty.
    /// </summary>
    public partial class MapUserVersionToXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) { }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
