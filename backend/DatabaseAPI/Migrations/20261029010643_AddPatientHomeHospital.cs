using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientHomeHospital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeHospitalId",
                table: "Patients",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_HomeHospitalId",
                table: "Patients",
                column: "HomeHospitalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Hospitals_HomeHospitalId",
                table: "Patients",
                column: "HomeHospitalId",
                principalTable: "Hospitals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Hospitals_HomeHospitalId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_HomeHospitalId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HomeHospitalId",
                table: "Patients");
        }
    }
}
