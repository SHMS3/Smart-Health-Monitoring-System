using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAlertSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAlertSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    EmergencyRiskLevelThreshold = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)3),
                    EmergencyRiskScoreThreshold = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.70m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedByAdminId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAlertSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAlertSettings_UpdatedByAdmin",
                        column: x => x.UpdatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "AiAlertSettings",
                columns: new[] { "Id", "CreatedAt", "EmergencyRiskLevelThreshold", "EmergencyRiskScoreThreshold", "UpdatedAt", "UpdatedByAdminId" },
                values: new object[] { 1, new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Local), (byte)3, 0.70m, new DateTime(2026, 6, 29, 0, 0, 0, 0, DateTimeKind.Local), null });

            migrationBuilder.CreateIndex(
                name: "IX_AiAlertSettings_UpdatedByAdminId",
                table: "AiAlertSettings",
                column: "UpdatedByAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAlertSettings");
        }
    }
}
