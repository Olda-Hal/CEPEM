using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrugTranslations");

            migrationBuilder.DropTable(
                name: "EventTypeTranslations");

            migrationBuilder.DropTable(
                name: "ExaminationTypeTranslations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "VaccineTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Symptoms");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "InjuryTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ExaminationTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "EventTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Drugs");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "DrugCategories");

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "VaccineTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "Symptoms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "InjuryTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "ExaminationTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "EventTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "Drugs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NameTranslationId",
                table: "DrugCategories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Translations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EN = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CS = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NL = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VaccineTypes_NameTranslationId",
                table: "VaccineTypes",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_Symptoms_NameTranslationId",
                table: "Symptoms",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NameTranslationId",
                table: "Roles",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_InjuryTypes_NameTranslationId",
                table: "InjuryTypes",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExaminationTypes_NameTranslationId",
                table: "ExaminationTypes",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventTypes_NameTranslationId",
                table: "EventTypes",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_NameTranslationId",
                table: "Drugs",
                column: "NameTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugCategories_NameTranslationId",
                table: "DrugCategories",
                column: "NameTranslationId");

            migrationBuilder.AddForeignKey(
                name: "FK_DrugCategories_Translations_NameTranslationId",
                table: "DrugCategories",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Drugs_Translations_NameTranslationId",
                table: "Drugs",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EventTypes_Translations_NameTranslationId",
                table: "EventTypes",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExaminationTypes_Translations_NameTranslationId",
                table: "ExaminationTypes",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InjuryTypes_Translations_NameTranslationId",
                table: "InjuryTypes",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Translations_NameTranslationId",
                table: "Roles",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Symptoms_Translations_NameTranslationId",
                table: "Symptoms",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VaccineTypes_Translations_NameTranslationId",
                table: "VaccineTypes",
                column: "NameTranslationId",
                principalTable: "Translations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DrugCategories_Translations_NameTranslationId",
                table: "DrugCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Drugs_Translations_NameTranslationId",
                table: "Drugs");

            migrationBuilder.DropForeignKey(
                name: "FK_EventTypes_Translations_NameTranslationId",
                table: "EventTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ExaminationTypes_Translations_NameTranslationId",
                table: "ExaminationTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_InjuryTypes_Translations_NameTranslationId",
                table: "InjuryTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Translations_NameTranslationId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Symptoms_Translations_NameTranslationId",
                table: "Symptoms");

            migrationBuilder.DropForeignKey(
                name: "FK_VaccineTypes_Translations_NameTranslationId",
                table: "VaccineTypes");

            migrationBuilder.DropTable(
                name: "Translations");

            migrationBuilder.DropIndex(
                name: "IX_VaccineTypes_NameTranslationId",
                table: "VaccineTypes");

            migrationBuilder.DropIndex(
                name: "IX_Symptoms_NameTranslationId",
                table: "Symptoms");

            migrationBuilder.DropIndex(
                name: "IX_Roles_NameTranslationId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_InjuryTypes_NameTranslationId",
                table: "InjuryTypes");

            migrationBuilder.DropIndex(
                name: "IX_ExaminationTypes_NameTranslationId",
                table: "ExaminationTypes");

            migrationBuilder.DropIndex(
                name: "IX_EventTypes_NameTranslationId",
                table: "EventTypes");

            migrationBuilder.DropIndex(
                name: "IX_Drugs_NameTranslationId",
                table: "Drugs");

            migrationBuilder.DropIndex(
                name: "IX_DrugCategories_NameTranslationId",
                table: "DrugCategories");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "VaccineTypes");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "Symptoms");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "InjuryTypes");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "ExaminationTypes");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "EventTypes");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "Drugs");

            migrationBuilder.DropColumn(
                name: "NameTranslationId",
                table: "DrugCategories");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "VaccineTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Symptoms",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Roles",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "InjuryTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ExaminationTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "EventTypes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Drugs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "DrugCategories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DrugTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DrugId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugTranslations_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EventTypeTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EventTypeId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTypeTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventTypeTranslations_EventTypes_EventTypeId",
                        column: x => x.EventTypeId,
                        principalTable: "EventTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExaminationTypeTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ExaminationTypeId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExaminationTypeTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExaminationTypeTranslations_ExaminationTypes_ExaminationType~",
                        column: x => x.ExaminationTypeId,
                        principalTable: "ExaminationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DrugTranslations_DrugId_Language",
                table: "DrugTranslations",
                columns: new[] { "DrugId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventTypeTranslations_EventTypeId_Language",
                table: "EventTypeTranslations",
                columns: new[] { "EventTypeId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExaminationTypeTranslations_ExaminationTypeId_Language",
                table: "ExaminationTypeTranslations",
                columns: new[] { "ExaminationTypeId", "Language" },
                unique: true);
        }
    }
}
