using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddPatientHabits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__PatientTh__Docto",
                table: "PatientThresholds");

            migrationBuilder.CreateTable(
                name: "PatientHabit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DietSalty = table.Column<bool>(type: "bit", nullable: false),
                    DietHighFat = table.Column<bool>(type: "bit", nullable: false),
                    DietHighSugar = table.Column<bool>(type: "bit", nullable: false),
                    DietLowFiber = table.Column<bool>(type: "bit", nullable: false),
                    AlcoholHeavy = table.Column<bool>(type: "bit", nullable: false),
                    CaffeineSpike = table.Column<bool>(type: "bit", nullable: false),
                    LifestyleSedentary = table.Column<bool>(type: "bit", nullable: false),
                    LifestyleSitLong = table.Column<bool>(type: "bit", nullable: false),
                    SleepDeprived = table.Column<bool>(type: "bit", nullable: false),
                    NoHealthCheck = table.Column<bool>(type: "bit", nullable: false),
                    SmokeActive = table.Column<bool>(type: "bit", nullable: false),
                    SmokePassive = table.Column<bool>(type: "bit", nullable: false),
                    SelfMedication = table.Column<bool>(type: "bit", nullable: false),
                    StressHigh = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientHabits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientHabit_Patient",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "Email",
                value: "    ");

            migrationBuilder.CreateIndex(
                name: "IX_PatientHabit_PatientId",
                table: "PatientHabit",
                column: "PatientId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientThreshold_Doctor",
                table: "PatientThresholds",
                column: "UpdatedByDoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientThreshold_Doctor",
                table: "PatientThresholds");

            migrationBuilder.DropTable(
                name: "PatientHabit");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "Email",
                value: "dr.an@smarthealth.vn");

            migrationBuilder.AddForeignKey(
                name: "FK__PatientTh__Docto",
                table: "PatientThresholds",
                column: "UpdatedByDoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
