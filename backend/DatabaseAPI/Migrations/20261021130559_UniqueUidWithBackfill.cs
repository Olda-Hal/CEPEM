using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUidWithBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UID",
                table: "Persons",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Backfill legacy empty-string UIDs with unique values so the unique
            // index can be created (MySQL unique indexes treat '' as a value)
            migrationBuilder.Sql("UPDATE Persons SET UID = CONCAT('legacy-', Id) WHERE UID = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_UID",
                table: "Persons",
                column: "UID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Persons_UID",
                table: "Persons");

            migrationBuilder.AlterColumn<string>(
                name: "UID",
                table: "Persons",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
