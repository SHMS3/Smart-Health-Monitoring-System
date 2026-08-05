using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddIsViewForPatientToClinicalRecord : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsViewForPatient",
                table: "ClinicalRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsViewForPatient",
                value: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsViewForPatient",
                value: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsViewForPatient",
                table: "ClinicalRecords");
        }
    }
}
