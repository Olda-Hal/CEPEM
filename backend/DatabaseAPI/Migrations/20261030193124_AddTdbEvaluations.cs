using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTdbEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "EvaluationCases",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PatientDocumentId",
                table: "EvaluationCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlicesJson",
                table: "EvaluationCases",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SourceConditions",
                table: "EvaluationCases",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SourcePosture",
                table: "EvaluationCases",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SourceSide",
                table: "EvaluationCases",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCases_PatientDocumentId",
                table: "EvaluationCases",
                column: "PatientDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationCases_PatientDocuments_PatientDocumentId",
                table: "EvaluationCases",
                column: "PatientDocumentId",
                principalTable: "PatientDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationCases_PatientDocuments_PatientDocumentId",
                table: "EvaluationCases");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationCases_PatientDocumentId",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "PatientDocumentId",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "SlicesJson",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "SourceConditions",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "SourcePosture",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "SourceSide",
                table: "EvaluationCases");
        }
    }
}
