using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class twoFieldForDailyVitalLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte>(
                name: "Status",
                table: "EmailNotifications",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "EmailNotifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSent",
                table: "EmailNotifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "EmailNotifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SentByDoctorId",
                table: "EmailNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToEmail",
                table: "EmailNotifications",
                type: "varchar(150)",
                unicode: false,
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsUpdateLocked",
                table: "DailyVitalLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "UpdateCount",
                table: "DailyVitalLogs",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "DailyVitalLogs",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "IsUpdateLocked", "UpdateCount" },
                values: new object[] { false, (byte)0 });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 1, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.hoa@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 2, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.minh@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 3, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.nhung@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 4, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.tam@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "ErrorMessage", "SentAt", "SentByDoctorId", "ToEmail" },
                values: new object[] { "SMTP timeout", null, null, "patient.long@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 6, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.thuy@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 7, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.hai@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ErrorMessage", "SentAt", "SentByDoctorId", "ToEmail" },
                values: new object[] { "Invalid address", null, null, "patient.yen@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 9, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.phong@gmail.com" });

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "ErrorMessage", "IsSent", "SentAt", "SentByDoctorId", "Status", "ToEmail" },
                values: new object[] { null, true, new DateTime(2026, 5, 20, 10, 7, 0, 0, DateTimeKind.Local), null, (byte)1, "patient.mai@gmail.com" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "IsSent",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "SentByDoctorId",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "ToEmail",
                table: "EmailNotifications");

            migrationBuilder.DropColumn(
                name: "IsUpdateLocked",
                table: "DailyVitalLogs");

            migrationBuilder.DropColumn(
                name: "UpdateCount",
                table: "DailyVitalLogs");

            migrationBuilder.AlterColumn<byte>(
                name: "Status",
                table: "EmailNotifications",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldDefaultValue: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 1,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 2,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 3,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 4,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 6,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 7,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 9,
                column: "Status",
                value: (byte)0);

            migrationBuilder.UpdateData(
                table: "EmailNotifications",
                keyColumn: "Id",
                keyValue: 10,
                column: "Status",
                value: (byte)0);
        }
    }
}
