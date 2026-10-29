using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Note = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceEventId = table.Column<int>(type: "int", nullable: true),
                    CreatedByEmployeeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CountryCode = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Events_SourceEventId",
                        column: x => x.SourceEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Visits_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VisitDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    PatientDocumentId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitDocuments_PatientDocuments_PatientDocumentId",
                        column: x => x.PatientDocumentId,
                        principalTable: "PatientDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VisitDocuments_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VisitDocuments_PatientDocumentId",
                table: "VisitDocuments",
                column: "PatientDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitDocuments_VisitId_PatientDocumentId",
                table: "VisitDocuments",
                columns: new[] { "VisitId", "PatientDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_PatientId",
                table: "Visits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SourceEventId",
                table: "Visits",
                column: "SourceEventId");

            // -----------------------------------------------------------------
            // Data backfill (no records are removed):
            // 1) Every existing Event becomes a Visit (note from its Comment).
            // 2) Patient documents are attached to per-day visits; visits are
            //    created per (patient, upload day) when no visit exists for
            //    that date yet (note "Přeneseno při migraci").
            // -----------------------------------------------------------------

            migrationBuilder.Sql(@"
INSERT INTO `Visits` (`PatientId`, `VisitDate`, `Note`, `SourceEventId`, `CreatedByEmployeeId`, `CreatedAt`, `CountryCode`)
SELECT e.`PatientId`, e.`HappenedAt`, c.`Text`, e.`Id`, NULL, UTC_TIMESTAMP(), e.`CountryCode`
FROM `Events` e
LEFT JOIN `Comments` c ON c.`Id` = e.`CommentId`;
");

            migrationBuilder.Sql(@"
INSERT INTO `Visits` (`PatientId`, `VisitDate`, `Note`, `SourceEventId`, `CreatedByEmployeeId`, `CreatedAt`, `CountryCode`)
SELECT d.`PatientId`, DATE(d.`UploadedAt`), 'Přeneseno při migraci', NULL, NULL, UTC_TIMESTAMP(), 'CZ'
FROM `PatientDocuments` d
WHERE NOT EXISTS (
    SELECT 1 FROM `Visits` v
    WHERE v.`PatientId` = d.`PatientId`
      AND DATE(v.`VisitDate`) = DATE(d.`UploadedAt`))
GROUP BY d.`PatientId`, DATE(d.`UploadedAt`);
");

            migrationBuilder.Sql(@"
INSERT INTO `VisitDocuments` (`VisitId`, `PatientDocumentId`, `SortOrder`, `CreatedAt`)
SELECT (
        SELECT v2.`Id`
        FROM `Visits` v2
        WHERE v2.`PatientId` = d.`PatientId`
          AND DATE(v2.`VisitDate`) = DATE(d.`UploadedAt`)
        ORDER BY
          CASE WHEN v2.`SourceEventId` IS NULL THEN 1 ELSE 0 END,
          v2.`Id`
        LIMIT 1),
       d.`Id`, 0, UTC_TIMESTAMP()
FROM `PatientDocuments` d
WHERE NOT EXISTS (
    SELECT 1 FROM `VisitDocuments` vd WHERE vd.`PatientDocumentId` = d.`Id`);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitDocuments");

            migrationBuilder.DropTable(
                name: "Visits");
        }
    }
}
