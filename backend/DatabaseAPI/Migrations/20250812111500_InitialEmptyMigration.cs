using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialEmptyMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Empty migration - database infrastructure ready for future entities
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty migration - no tables to drop
        }
    }
}
