using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCreatedByEmployeeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByEmployeeId",
                table: "Employees",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeId",
                table: "Employees");
        }
    }
}
