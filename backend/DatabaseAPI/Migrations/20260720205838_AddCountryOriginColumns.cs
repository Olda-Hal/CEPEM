using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryOriginColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "ReservationSlots",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "CZ")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Persons",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "CZ")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Patients",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "CZ")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Examinations",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "CZ")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Events",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "CZ")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "ReservationSlots");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Examinations");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Events");
        }
    }
}
