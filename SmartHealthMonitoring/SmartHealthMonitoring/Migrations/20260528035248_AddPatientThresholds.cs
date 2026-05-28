using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartHealthMonitoring.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientThresholds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    SystolicBpWarning = table.Column<short>(type: "smallint", nullable: false),
                    SystolicBpDanger = table.Column<short>(type: "smallint", nullable: false),
                    DiastolicBpWarning = table.Column<short>(type: "smallint", nullable: false),
                    DiastolicBpDanger = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateWarningMin = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateDangerMin = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateWarningMax = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateDangerMax = table.Column<short>(type: "smallint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedByDoctorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PatientThresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK__PatientTh__Docto",
                        column: x => x.UpdatedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK__PatientTh__Patie",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PatientThresholds",
                columns: new[] { "Id", "DiastolicBpDanger", "DiastolicBpWarning", "HeartRateDangerMax", "HeartRateDangerMin", "HeartRateWarningMax", "HeartRateWarningMin", "PatientId", "SystolicBpDanger", "SystolicBpWarning", "UpdatedAt", "UpdatedByDoctorId" },
                values: new object[,]
                {
                    { 1, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 1, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 2, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 3, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 3, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 4, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 4, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 5, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 5, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 6, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 6, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 7, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 7, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 8, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 8, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 9, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 9, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 10, (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, 10, (short)140, (short)130, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientThresholds_PatientId",
                table: "PatientThresholds",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientThresholds_UpdatedByDoctorId",
                table: "PatientThresholds",
                column: "UpdatedByDoctorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientThresholds");
        }
    }
}
