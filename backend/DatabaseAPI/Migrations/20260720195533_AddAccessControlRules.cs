using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessControlRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessControlVersion",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "EmployeePermissionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PermissionKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Effect = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePermissionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePermissionRules_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RolePermissionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Effect = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissionRules_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeePermissionScopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeePermissionRuleId = table.Column<int>(type: "int", nullable: false),
                    ResourceType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResourceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePermissionScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePermissionScopes_EmployeePermissionRules_EmployeePer~",
                        column: x => x.EmployeePermissionRuleId,
                        principalTable: "EmployeePermissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RolePermissionScopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RolePermissionRuleId = table.Column<int>(type: "int", nullable: false),
                    ResourceType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResourceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissionScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissionScopes_RolePermissionRules_RolePermissionRuleId",
                        column: x => x.RolePermissionRuleId,
                        principalTable: "RolePermissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePermissionRules_EmployeeId_PermissionKey",
                table: "EmployeePermissionRules",
                columns: new[] { "EmployeeId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePermissionScopes_EmployeePermissionRuleId_ResourceTy~",
                table: "EmployeePermissionScopes",
                columns: new[] { "EmployeePermissionRuleId", "ResourceType", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissionRules_RoleId_PermissionKey",
                table: "RolePermissionRules",
                columns: new[] { "RoleId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissionScopes_RolePermissionRuleId_ResourceType_Resou~",
                table: "RolePermissionScopes",
                columns: new[] { "RolePermissionRuleId", "ResourceType", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePermissionScopes");

            migrationBuilder.DropTable(
                name: "RolePermissionScopes");

            migrationBuilder.DropTable(
                name: "EmployeePermissionRules");

            migrationBuilder.DropTable(
                name: "RolePermissionRules");

            migrationBuilder.DropColumn(
                name: "AccessControlVersion",
                table: "Employees");
        }
    }
}
