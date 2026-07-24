using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartHealthMonitoring.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleAppointmentsPerSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_SlotId",
                table: "Appointments");

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Description", "IsActive", "Name", "Price" },
                values: new object[,]
                {
                    { 1, "Loại đau ngực (Chest Pain Type)|Đau ngực gắng sức (Exercise Angina)|Huyết áp nghỉ (Resting BP - mmHg)|Nhịp tim tối đa (Max Heart Rate)", true, "Huyết áp & Triệu chứng", 150000.00m },
                    { 2, "Cholesterol toàn phần (mg/dL)|Đường huyết lúc đói (Fasting Blood Sugar)|Kết quả Thal (Thalassemia)", true, "Phân tích Huyết học", 200000.00m },
                    { 3, "Điện tâm đồ nghỉ (Resting ECG)|Độ trầm cảm đoạn ST (OldPeak)|Độ dốc đoạn ST (ST Slope)|Số mạch vành chính (Major Vessels)|Ảnh ECG (tải lên từ máy)", true, "Điện tâm đồ & Mạch vành", 250000.00m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SlotId",
                table: "Appointments",
                column: "SlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_SlotId",
                table: "Appointments");

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SlotId",
                table: "Appointments",
                column: "SlotId",
                unique: true);
        }
    }
}
