using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddAttachmentUrlToClinicalRecord : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "ClinicalRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 1,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 2,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 3,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 4,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 5,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 6,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 7,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 8,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 9,
                column: "AttachmentUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ClinicalRecords",
                keyColumn: "Id",
                keyValue: 10,
                column: "AttachmentUrl",
                value: null);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "ClinicalRecords");
        }
    }
}
