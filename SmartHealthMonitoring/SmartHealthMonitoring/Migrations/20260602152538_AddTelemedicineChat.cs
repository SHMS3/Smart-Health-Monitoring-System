using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    public partial class AddTelemedicineChat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelemedicineChatSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientUserId = table.Column<int>(type: "int", nullable: false),
                    DoctorUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemedicineChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemedicineSession_Doctor",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TelemedicineSession_Patient",
                        column: x => x.PatientUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TelemedicineChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    ReceiverId = table.Column<int>(type: "int", nullable: false),
                    MessageContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemedicineChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemedicineChat_Receiver",
                        column: x => x.ReceiverId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TelemedicineChat_Sender",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TelemedicineChat_Session",
                        column: x => x.SessionId,
                        principalTable: "TelemedicineChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelemedicineChat_Session_Time",
                table: "TelemedicineChatMessages",
                columns: new[] { "SessionId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemedicineChatMessages_ReceiverId",
                table: "TelemedicineChatMessages",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_TelemedicineChatMessages_SenderId",
                table: "TelemedicineChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TelemedicineSession_Doctor",
                table: "TelemedicineChatSessions",
                columns: new[] { "DoctorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemedicineSession_Patient",
                table: "TelemedicineChatSessions",
                columns: new[] { "PatientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemedicineSession_Status",
                table: "TelemedicineChatSessions",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemedicineChatMessages");

            migrationBuilder.DropTable(
                name: "TelemedicineChatSessions");
        }
    }
}
