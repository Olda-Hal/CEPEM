using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceReservationPatientWithPerson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Patients_PatientId",
                table: "Reservations");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "Reservations",
                newName: "PersonId");

            migrationBuilder.RenameIndex(
                name: "IX_Reservations_PatientId",
                table: "Reservations",
                newName: "IX_Reservations_PersonId");

            migrationBuilder.Sql(@"
                UPDATE Reservations r
                INNER JOIN Patients p ON r.PersonId = p.Id
                SET r.PersonId = p.PersonId;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Persons_PersonId",
                table: "Reservations",
                column: "PersonId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Persons_PersonId",
                table: "Reservations");

            migrationBuilder.RenameColumn(
                name: "PersonId",
                table: "Reservations",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_Reservations_PersonId",
                table: "Reservations",
                newName: "IX_Reservations_PatientId");

            migrationBuilder.Sql(@"
                UPDATE Reservations r
                INNER JOIN Patients p ON r.PatientId = p.PersonId
                SET r.PatientId = p.Id;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Patients_PatientId",
                table: "Reservations",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
