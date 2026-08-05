using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddDobSexToDoctor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Doctors",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Sex",
                table: "Doctors",
                type: "tinyint",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "DateOfBirth", "Sex" },
                values: new object[] { null, null });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Doctors");
        }
    }
}
