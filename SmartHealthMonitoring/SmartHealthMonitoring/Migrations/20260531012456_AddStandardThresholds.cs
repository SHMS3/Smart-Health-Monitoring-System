using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddStandardThresholds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandardThresholds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Sex = table.Column<byte>(type: "tinyint", nullable: false),
                    AgeMin = table.Column<byte>(type: "tinyint", nullable: false),
                    AgeMax = table.Column<byte>(type: "tinyint", nullable: false),
                    SystolicBpWarning = table.Column<short>(type: "smallint", nullable: false),
                    SystolicBpDanger = table.Column<short>(type: "smallint", nullable: false),
                    DiastolicBpWarning = table.Column<short>(type: "smallint", nullable: false),
                    DiastolicBpDanger = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateWarningMin = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateDangerMin = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateWarningMax = table.Column<short>(type: "smallint", nullable: false),
                    HeartRateDangerMax = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StandardThresholds", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "StandardThresholds",
                columns: new[] { "Id", "AgeMax", "AgeMin", "CreatedAt", "Description", "DiastolicBpDanger", "DiastolicBpWarning", "HeartRateDangerMax", "HeartRateDangerMin", "HeartRateWarningMax", "HeartRateWarningMin", "IsActive", "Name", "Sex", "SystolicBpDanger", "SystolicBpWarning", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, (byte)17, (byte)0, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "�p d?ng cho c? nam v� n? t? 0�17 tu?i theo khuy?n ngh? AAP/WHO", (short)85, (short)75, (short)120, (short)55, (short)100, (short)65, true, "Tr? em & Thanh thi?u ni�n (= 17 tu?i)", (byte)2, (short)130, (short)120, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) },
                    { 2, (byte)40, (byte)18, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "Ngu?ng chu?n cho nam gi?i tru?ng th�nh theo JNC8/WHO", (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, true, "Nam 18�40 tu?i", (byte)1, (short)140, (short)130, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) },
                    { 3, (byte)40, (byte)18, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "Ngu?ng chu?n cho n? gi?i tru?ng th�nh theo JNC8/WHO", (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, true, "N? 18�40 tu?i", (byte)0, (short)140, (short)130, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) },
                    { 4, (byte)60, (byte)41, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "Ngu?ng chu?n cho nam gi?i trung ni�n, nguy co tim m?ch tang", (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, true, "Nam 41�60 tu?i", (byte)1, (short)140, (short)130, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) },
                    { 5, (byte)60, (byte)41, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "Ngu?ng chu?n cho n? gi?i trung ni�n (giai do?n ti?n m�n kinh)", (short)90, (short)80, (short)120, (short)50, (short)100, (short)60, true, "N? 41�60 tu?i", (byte)0, (short)140, (short)130, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) },
                    { 6, (byte)120, (byte)61, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "Ngu?ng di?u ch?nh cho nam cao tu?i (huy?t �p m?c ti�u cao hon theo ESC 2023)", (short)90, (short)85, (short)110, (short)45, (short)95, (short)55, true, "Nam tr�n 60 tu?i", (byte)1, (short)150, (short)140, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) },
                    { 7, (byte)120, (byte)61, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local), "Ngu?ng di?u ch?nh cho n? cao tu?i (huy?t �p m?c ti�u cao hon theo ESC 2023)", (short)90, (short)85, (short)110, (short)45, (short)95, (short)55, true, "N? tr�n 60 tu?i", (byte)0, (short)150, (short)140, new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Local) }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardThresholds");
        }
    }
}
