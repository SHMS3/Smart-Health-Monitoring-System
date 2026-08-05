using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddCitizenIdToDoctorAndPatient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                table: "Patients",
                type: "varchar(12)",
                unicode: false,
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                table: "Doctors",
                type: "varchar(12)",
                unicode: false,
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeLicense",
                table: "Doctors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CitizenId", "PracticeLicense" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 1,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 2,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 3,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 4,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 5,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 6,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 7,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 8,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 9,
                column: "CitizenId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: 10,
                column: "CitizenId",
                value: null);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CitizenId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CitizenId",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "PracticeLicense",
                table: "Doctors");
        }
    }
}
