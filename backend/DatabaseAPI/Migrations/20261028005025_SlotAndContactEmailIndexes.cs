using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class SlotAndContactEmailIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservationSlots_HospitalId",
                table: "ReservationSlots");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ContactEmails",
                type: "varchar(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSlots_HospitalId_StartDateTime",
                table: "ReservationSlots",
                columns: new[] { "HospitalId", "StartDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactEmails_Email",
                table: "ContactEmails",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservationSlots_HospitalId_StartDateTime",
                table: "ReservationSlots");

            migrationBuilder.DropIndex(
                name: "IX_ContactEmails_Email",
                table: "ContactEmails");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ContactEmails",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(320)",
                oldMaxLength: 320)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSlots_HospitalId",
                table: "ReservationSlots",
                column: "HospitalId");
        }
    }
}
