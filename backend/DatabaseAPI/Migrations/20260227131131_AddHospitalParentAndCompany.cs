using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalParentAndCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyIco",
                table: "Hospitals",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Hospitals",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ParentHospitalId",
                table: "Hospitals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_ParentHospitalId",
                table: "Hospitals",
                column: "ParentHospitalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hospitals_Hospitals_ParentHospitalId",
                table: "Hospitals",
                column: "ParentHospitalId",
                principalTable: "Hospitals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hospitals_Hospitals_ParentHospitalId",
                table: "Hospitals");

            migrationBuilder.DropIndex(
                name: "IX_Hospitals_ParentHospitalId",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "CompanyIco",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "ParentHospitalId",
                table: "Hospitals");
        }
    }
}
