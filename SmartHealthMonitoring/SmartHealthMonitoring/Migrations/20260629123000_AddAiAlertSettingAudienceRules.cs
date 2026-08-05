using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartHealthMonitoring.Context;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    [DbContext(typeof(SmartHealthMonitoringContext))]
    [Migration("20260629123000_AddAiAlertSettingAudienceRules")]
    public partial class AddAiAlertSettingAudienceRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "EmergencyAgeMax",
                table: "AiAlertSettings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)120);

            migrationBuilder.AddColumn<byte>(
                name: "EmergencyAgeMin",
                table: "AiAlertSettings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "EmergencySex",
                table: "AiAlertSettings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)2);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmergencyAgeMax",
                table: "AiAlertSettings");

            migrationBuilder.DropColumn(
                name: "EmergencyAgeMin",
                table: "AiAlertSettings");

            migrationBuilder.DropColumn(
                name: "EmergencySex",
                table: "AiAlertSettings");
        }
    }
}
