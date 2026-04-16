using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFormSubmissionAndSicknessHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FormSubmissionConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FormSubmissionId = table.Column<int>(type: "int", nullable: false),
                    ConfirmAccuracy = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TermsAccepted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SignaturePlace = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SignatureDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SignatureVector = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissionConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissionConsents_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FormSubmissionLifestyles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FormSubmissionId = table.Column<int>(type: "int", nullable: false),
                    PoorSleep = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DigestiveIssues = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PhysicalStress = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MentalStress = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Smoking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Fatigue = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastMealHours = table.Column<float>(type: "float", nullable: true),
                    VaccinesAfter2023 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AdditionalHealthInfo = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissionLifestyles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissionLifestyles_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FormSubmissionMedications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FormSubmissionId = table.Column<int>(type: "int", nullable: false),
                    MedBloodPressure = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedHeart = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedCholesterol = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedBloodThinners = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedDiabetes = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedThyroid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedNerves = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedPsych = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedDigestion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedPain = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedDehydration = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedBreathing = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedAntibiotics = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedSupplements = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MedAllergies = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissionMedications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissionMedications_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FormSubmissionReproductiveHealths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FormSubmissionId = table.Column<int>(type: "int", nullable: false),
                    LastMenstruationDate = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MenstruationCycleDays = table.Column<int>(type: "int", nullable: true),
                    YearsSinceLastMenstruation = table.Column<int>(type: "int", nullable: true),
                    GaveBirth = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BirthCount = table.Column<int>(type: "int", nullable: true),
                    BirthWhen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Breastfed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BreastfeedingMonths = table.Column<int>(type: "int", nullable: true),
                    BreastfeedingInflammation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EndedWithInflammation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Contraception = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ContraceptionDuration = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Estrogen = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EstrogenType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Interruption = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    InterruptionCount = table.Column<int>(type: "int", nullable: true),
                    Miscarriage = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MiscarriageCount = table.Column<int>(type: "int", nullable: true),
                    BreastInjury = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Mammogram = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MammogramCount = table.Column<int>(type: "int", nullable: true),
                    BreastBiopsy = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BreastImplants = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BreastSurgery = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BreastSurgeryType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FamilyTumors = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FamilyTumorType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissionReproductiveHealths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissionReproductiveHealths_FormSubmissions_FormSubmis~",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SicknessHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SicknessName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HadSickness = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    SicknessWhen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Vaccinated = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    VaccinationWhen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FormSubmissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SicknessHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SicknessHistories_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissionConsents_FormSubmissionId",
                table: "FormSubmissionConsents",
                column: "FormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissionLifestyles_FormSubmissionId",
                table: "FormSubmissionLifestyles",
                column: "FormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissionMedications_FormSubmissionId",
                table: "FormSubmissionMedications",
                column: "FormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissionReproductiveHealths_FormSubmissionId",
                table: "FormSubmissionReproductiveHealths",
                column: "FormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_EventId",
                table: "FormSubmissions",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_PatientId",
                table: "FormSubmissions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_SicknessHistories_FormSubmissionId",
                table: "SicknessHistories",
                column: "FormSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormSubmissionConsents");

            migrationBuilder.DropTable(
                name: "FormSubmissionLifestyles");

            migrationBuilder.DropTable(
                name: "FormSubmissionMedications");

            migrationBuilder.DropTable(
                name: "FormSubmissionReproductiveHealths");

            migrationBuilder.DropTable(
                name: "SicknessHistories");

            migrationBuilder.DropTable(
                name: "FormSubmissions");
        }
    }
}
