using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VisitId",
                table: "EvaluationCases",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EvaluationReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EvaluationCaseId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    AnnotationsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Recommendation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Outcome = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationReviews_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EvaluationReviews_EvaluationCases_EvaluationCaseId",
                        column: x => x.EvaluationCaseId,
                        principalTable: "EvaluationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCases_VisitId",
                table: "EvaluationCases",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReviews_EmployeeId",
                table: "EvaluationReviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReviews_EvaluationCaseId_EmployeeId",
                table: "EvaluationReviews",
                columns: new[] { "EvaluationCaseId", "EmployeeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationCases_Visits_VisitId",
                table: "EvaluationCases",
                column: "VisitId",
                principalTable: "Visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Existing single evaluations become the first review.
            migrationBuilder.Sql(
                """
                INSERT INTO EvaluationReviews
                    (EvaluationCaseId, EmployeeId, AnnotationsJson, Recommendation, Outcome, EvaluatedAtUtc, CreatedAtUtc)
                SELECT
                    Id, EvaluatedByEmployeeId, AnnotationsJson, Recommendation, Outcome, EvaluatedAtUtc, EvaluatedAtUtc
                FROM EvaluationCases
                WHERE EvaluatedAtUtc IS NOT NULL;
                """);

            // Backfill the visit link for .tdb cases uploaded before VisitId existed.
            migrationBuilder.Sql(
                """
                UPDATE EvaluationCases c
                JOIN VisitDocuments vd ON vd.PatientDocumentId = c.PatientDocumentId
                SET c.VisitId = vd.VisitId
                WHERE c.PatientDocumentId IS NOT NULL AND c.VisitId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationCases_Visits_VisitId",
                table: "EvaluationCases");

            migrationBuilder.DropTable(
                name: "EvaluationReviews");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationCases_VisitId",
                table: "EvaluationCases");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "EvaluationCases");
        }
    }
}
