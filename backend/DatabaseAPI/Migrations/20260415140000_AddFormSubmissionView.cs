using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFormSubmissionView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE VIEW vw_FormSubmission_Complete AS
                SELECT
                    fs.Id AS FormSubmissionId,
                    fs.PatientId,
                    fs.EventId,
                    fs.SubmittedAtUtc,
                    fsc.Id AS ConsentId,
                    fsc.ConfirmAccuracy,
                    fsc.TermsAccepted,
                    fsc.SignaturePlace,
                    fsc.SignatureDate,
                    fsc.SignatureVector,
                    fsm.Id AS MedicationId,
                    fsm.MedBloodPressure,
                    fsm.MedHeart,
                    fsm.MedCholesterol,
                    fsm.MedBloodThinners,
                    fsm.MedDiabetes,
                    fsm.MedThyroid,
                    fsm.MedNerves,
                    fsm.MedPsych,
                    fsm.MedDigestion,
                    fsm.MedPain,
                    fsm.MedDehydration,
                    fsm.MedBreathing,
                    fsm.MedAntibiotics,
                    fsm.MedSupplements,
                    fsm.MedAllergies,
                    fsl.Id AS LifestyleId,
                    fsl.PoorSleep,
                    fsl.DigestiveIssues,
                    fsl.PhysicalStress,
                    fsl.MentalStress,
                    fsl.Smoking,
                    fsl.Fatigue,
                    fsl.LastMealHours,
                    fsl.VaccinesAfter2023,
                    fsl.AdditionalHealthInfo,
                    fsrh.Id AS ReproductiveHealthId,
                    fsrh.LastMenstruationDate,
                    fsrh.MenstruationCycleDays,
                    fsrh.YearsSinceLastMenstruation,
                    fsrh.GaveBirth,
                    fsrh.BirthCount,
                    fsrh.BirthWhen,
                    fsrh.Breastfed,
                    fsrh.BreastfeedingMonths,
                    fsrh.BreastfeedingInflammation,
                    fsrh.EndedWithInflammation,
                    fsrh.Contraception,
                    fsrh.ContraceptionDuration,
                    fsrh.Estrogen,
                    fsrh.EstrogenType,
                    fsrh.Interruption,
                    fsrh.InterruptionCount,
                    fsrh.Miscarriage,
                    fsrh.MiscarriageCount,
                    fsrh.BreastInjury,
                    fsrh.Mammogram,
                    fsrh.MammogramCount,
                    fsrh.BreastBiopsy,
                    fsrh.BreastImplants,
                    fsrh.BreastSurgery,
                    fsrh.BreastSurgeryType,
                    fsrh.FamilyTumors,
                    fsrh.FamilyTumorType
                FROM
                    FormSubmissions fs
                    LEFT JOIN FormSubmissionConsents fsc ON fs.Id = fsc.FormSubmissionId
                    LEFT JOIN FormSubmissionMedications fsm ON fs.Id = fsm.FormSubmissionId
                    LEFT JOIN FormSubmissionLifestyles fsl ON fs.Id = fsl.FormSubmissionId
                    LEFT JOIN FormSubmissionReproductiveHealths fsrh ON fs.Id = fsrh.FormSubmissionId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_FormSubmission_Complete;");
        }
    }
}
