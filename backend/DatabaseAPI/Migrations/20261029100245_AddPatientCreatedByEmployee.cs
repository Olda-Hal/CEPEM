using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientCreatedByEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByEmployeeId",
                table: "Patients",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedByEmployeeId",
                table: "Patients",
                column: "CreatedByEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Employees_CreatedByEmployeeId",
                table: "Patients",
                column: "CreatedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Employees_CreatedByEmployeeId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_CreatedByEmployeeId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeId",
                table: "Patients");
        }
    }
}
