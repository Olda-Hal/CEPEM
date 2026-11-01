using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitIntakeScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservationSlotId",
                table: "Visits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IntakeReminderSentAtUtc",
                table: "ReservationSlots",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSubmittedAtUtc",
                table: "IntakeFormLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservationSlotId",
                table: "IntakeFormLinks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisitId",
                table: "IntakeFormLinks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CovidVaccine",
                table: "FormSubmissionLifestyles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CovidWhen",
                table: "FormSubmissionLifestyles",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "HadCovid",
                table: "FormSubmissionLifestyles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "Height",
                table: "FormSubmissionLifestyles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Weight",
                table: "FormSubmissionLifestyles",
                type: "float",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ReservationSlotId",
                table: "Visits",
                column: "ReservationSlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeFormLinks_ReservationSlotId",
                table: "IntakeFormLinks",
                column: "ReservationSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeFormLinks_VisitId",
                table: "IntakeFormLinks",
                column: "VisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeFormLinks_ReservationSlots_ReservationSlotId",
                table: "IntakeFormLinks",
                column: "ReservationSlotId",
                principalTable: "ReservationSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeFormLinks_Visits_VisitId",
                table: "IntakeFormLinks",
                column: "VisitId",
                principalTable: "Visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_ReservationSlots_ReservationSlotId",
                table: "Visits",
                column: "ReservationSlotId",
                principalTable: "ReservationSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeFormLinks_ReservationSlots_ReservationSlotId",
                table: "IntakeFormLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeFormLinks_Visits_VisitId",
                table: "IntakeFormLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_Visits_ReservationSlots_ReservationSlotId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ReservationSlotId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_IntakeFormLinks_ReservationSlotId",
                table: "IntakeFormLinks");

            migrationBuilder.DropIndex(
                name: "IX_IntakeFormLinks_VisitId",
                table: "IntakeFormLinks");

            migrationBuilder.DropColumn(
                name: "ReservationSlotId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "IntakeReminderSentAtUtc",
                table: "ReservationSlots");

            migrationBuilder.DropColumn(
                name: "LastSubmittedAtUtc",
                table: "IntakeFormLinks");

            migrationBuilder.DropColumn(
                name: "ReservationSlotId",
                table: "IntakeFormLinks");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "IntakeFormLinks");

            migrationBuilder.DropColumn(
                name: "CovidVaccine",
                table: "FormSubmissionLifestyles");

            migrationBuilder.DropColumn(
                name: "CovidWhen",
                table: "FormSubmissionLifestyles");

            migrationBuilder.DropColumn(
                name: "HadCovid",
                table: "FormSubmissionLifestyles");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "FormSubmissionLifestyles");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "FormSubmissionLifestyles");
        }
    }
}
