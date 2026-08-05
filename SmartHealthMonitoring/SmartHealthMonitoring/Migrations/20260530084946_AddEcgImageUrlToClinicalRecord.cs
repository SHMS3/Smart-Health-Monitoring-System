using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddEcgImageUrlToClinicalRecord : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EcgImageUrl",
                table: "ClinicalRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 1,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 2,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 3,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 4,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 5,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 6,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 7,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 8,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 9,
                column: "EcgImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 10,
                column: "EcgImageUrl",
                value: null);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EcgImageUrl",
                table: "ClinicalRecords");
        }
    }
}
