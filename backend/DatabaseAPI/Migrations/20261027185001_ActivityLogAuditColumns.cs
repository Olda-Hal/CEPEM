using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class ActivityLogAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeEmail",
                table: "ActivityLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "ActivityLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "ActivityLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "ActivityLogs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeEmail",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "ActivityLogs");
        }
    }
}
