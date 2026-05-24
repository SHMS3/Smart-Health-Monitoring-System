using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartHealthMonitoring.Migrations
{
    /// <inheritdoc />
    public partial class Initial_V4_With_SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__3214EC07EBF30E0D", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Tim mạch"),
                    IsOnShift = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Doctors__3214EC07A1A2F30A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Doctors__UserId__4222D4EF",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Sex = table.Column<byte>(type: "tinyint", nullable: false),
                    Phone = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Patients__3214EC07383EFB80", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Patients__UserId__3D5E1FD2",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatbotSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ContextVitals = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ChatbotS__3214EC07FA91225A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ChatbotSe__Patie__7B5B524B",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClinicalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ChestPainType = table.Column<byte>(type: "tinyint", nullable: false),
                    RestingBP = table.Column<short>(type: "smallint", nullable: false),
                    Cholesterol = table.Column<short>(type: "smallint", nullable: false),
                    FastingBS = table.Column<byte>(type: "tinyint", nullable: false),
                    RestECG = table.Column<byte>(type: "tinyint", nullable: false),
                    MaxHeartRate = table.Column<short>(type: "smallint", nullable: false),
                    ExerciseAngina = table.Column<byte>(type: "tinyint", nullable: false),
                    OldPeak = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    STSlope = table.Column<byte>(type: "tinyint", nullable: false),
                    MajorVessels = table.Column<byte>(type: "tinyint", nullable: false),
                    ThalResult = table.Column<byte>(type: "tinyint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Clinical__3214EC07E439AAE9", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ClinicalR__Docto__48CFD27E",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ClinicalR__Patie__47DBAE45",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DailyVitalLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    SystolicBP = table.Column<short>(type: "smallint", nullable: false),
                    DiastolicBP = table.Column<short>(type: "smallint", nullable: false),
                    HeartRate = table.Column<short>(type: "smallint", nullable: false),
                    ChestPainLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    HasExerciseAngina = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DailyVit__3214EC075CD951CD", x => x.Id);
                    table.ForeignKey(
                        name: "FK__DailyVita__Patie__5812160E",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    SenderRole = table.Column<byte>(type: "tinyint", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ChatMess__3214EC07DF8E26C2", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ChatMessa__Sessi__7F2BE32F",
                        column: x => x.SessionId,
                        principalTable: "ChatbotSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AIRiskPredictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ClinicalRecordId = table.Column<int>(type: "int", nullable: true),
                    DailyLogId = table.Column<int>(type: "int", nullable: true),
                    RiskScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PredictedTarget = table.Column<byte>(type: "tinyint", nullable: false),
                    RiskLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    ModelVersion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "v1.0"),
                    PredictedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AIRiskPr__3214EC076EE87583", x => x.Id);
                    table.ForeignKey(
                        name: "FK__AIRiskPre__Clini__6383C8BA",
                        column: x => x.ClinicalRecordId,
                        principalTable: "ClinicalRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__AIRiskPre__Daily__6477ECF3",
                        column: x => x.DailyLogId,
                        principalTable: "DailyVitalLogs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__AIRiskPre__Patie__628FA481",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WarningAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    PredictionId = table.Column<int>(type: "int", nullable: false),
                    ClaimedByDoctorId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__WarningA__3214EC071EB1CBC8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__WarningAl__Claim__6FE99F9F",
                        column: x => x.ClaimedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WarningAl__Patie__6E01572D",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WarningAl__Predi__6EF57B66",
                        column: x => x.PredictionId,
                        principalTable: "AIRiskPredictions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmailNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__EmailNot__3214EC0730D50919", x => x.Id);
                    table.ForeignKey(
                        name: "FK__EmailNoti__Alert__75A278F5",
                        column: x => x.AlertId,
                        principalTable: "WarningAlerts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__EmailNoti__Patie__76969D2E",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FullName", "IsDeleted", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.an@smarthealth.vn", "Nguyễn Văn An", false, "hash123", (byte)1 },
                    { 2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.binh@smarthealth.vn", "Trần Thị Bình", false, "hash123", (byte)1 },
                    { 3, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.cuong@smarthealth.vn", "Phạm Minh Cường", false, "hash123", (byte)1 },
                    { 4, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.dung@smarthealth.vn", "Lê Tuấn Dũng", false, "hash123", (byte)1 },
                    { 5, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.anh@smarthealth.vn", "Hoàng Mai Anh", false, "hash123", (byte)1 },
                    { 6, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.phuc@smarthealth.vn", "Đinh Văn Phúc", false, "hash123", (byte)1 },
                    { 7, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.chau@smarthealth.vn", "Nguyễn Bảo Châu", false, "hash123", (byte)1 },
                    { 8, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.khanh@smarthealth.vn", "Vũ Quốc Khánh", false, "hash123", (byte)1 },
                    { 9, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.tuan@smarthealth.vn", "Bùi Anh Tuấn", false, "hash123", (byte)1 },
                    { 10, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "dr.lan@smarthealth.vn", "Lý Phương Lan", false, "hash123", (byte)1 },
                    { 11, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.hoa@gmail.com", "Nguyễn Thị Hoa", false, "hash123", (byte)0 },
                    { 12, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.minh@gmail.com", "Trần Đức Minh", false, "hash123", (byte)0 },
                    { 13, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.nhung@gmail.com", "Đỗ Hồng Nhung", false, "hash123", (byte)0 },
                    { 14, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.tam@gmail.com", "Bùi Văn Tâm", false, "hash123", (byte)0 },
                    { 15, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.long@gmail.com", "Phạm Thành Long", false, "hash123", (byte)0 },
                    { 16, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.thuy@gmail.com", "Lê Thanh Thủy", false, "hash123", (byte)0 },
                    { 17, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.hai@gmail.com", "Đặng Quang Hải", false, "hash123", (byte)0 },
                    { 18, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.yen@gmail.com", "Võ Hoàng Yến", false, "hash123", (byte)0 },
                    { 19, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.phong@gmail.com", "Ngô Đình Phong", false, "hash123", (byte)0 },
                    { 20, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc), "patient.mai@gmail.com", "Đoàn Ngọc Mai", false, "hash123", (byte)0 }
                });

            migrationBuilder.InsertData(
                table: "Doctors",
                columns: new[] { "Id", "IsDeleted", "IsOnShift", "Specialty", "UserId" },
                values: new object[,]
                {
                    { 1, false, true, "Tim mạch can thiệp", 1 },
                    { 2, false, false, "Nhịp học tim mạch", 2 },
                    { 3, false, true, "Nội tim mạch", 3 },
                    { 4, false, false, "Phẫu thuật tim", 4 },
                    { 5, false, true, "Nội tim mạch", 5 },
                    { 6, false, false, "Tim mạch nhi", 6 },
                    { 7, false, true, "Chẩn đoán hình ảnh", 7 },
                    { 8, false, false, "Nội tim mạch", 8 },
                    { 9, false, true, "Tim mạch can thiệp", 9 },
                    { 10, false, false, "Nội tim mạch", 10 }
                });

            migrationBuilder.InsertData(
                table: "Patients",
                columns: new[] { "Id", "DateOfBirth", "IsDeleted", "Phone", "Sex", "UserId" },
                values: new object[,]
                {
                    { 1, new DateOnly(1965, 4, 12), false, "0912345671", (byte)0, 11 },
                    { 2, new DateOnly(1978, 8, 22), false, "0912345672", (byte)1, 12 },
                    { 3, new DateOnly(1955, 11, 5), false, "0912345673", (byte)0, 13 },
                    { 4, new DateOnly(1982, 2, 17), false, "0912345674", (byte)1, 14 },
                    { 5, new DateOnly(1960, 7, 30), false, "0912345675", (byte)1, 15 },
                    { 6, new DateOnly(1990, 9, 14), false, "0912345676", (byte)0, 16 },
                    { 7, new DateOnly(1950, 3, 25), false, "0912345677", (byte)1, 17 },
                    { 8, new DateOnly(1975, 1, 10), false, "0912345678", (byte)0, 18 },
                    { 9, new DateOnly(1988, 6, 5), false, "0912345679", (byte)1, 19 },
                    { 10, new DateOnly(1962, 12, 12), false, "0912345680", (byte)0, 20 }
                });

            migrationBuilder.InsertData(
                table: "ChatbotSessions",
                columns: new[] { "Id", "ContextVitals", "PatientId", "StartedAt" },
                values: new object[,]
                {
                    { 1, null, 1, new DateTime(2026, 5, 20, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, null, 2, new DateTime(2026, 5, 20, 12, 10, 0, 0, DateTimeKind.Utc) },
                    { 3, null, 3, new DateTime(2026, 5, 20, 12, 20, 0, 0, DateTimeKind.Utc) },
                    { 4, null, 4, new DateTime(2026, 5, 20, 12, 30, 0, 0, DateTimeKind.Utc) },
                    { 5, null, 5, new DateTime(2026, 5, 20, 12, 40, 0, 0, DateTimeKind.Utc) },
                    { 6, null, 6, new DateTime(2026, 5, 20, 12, 50, 0, 0, DateTimeKind.Utc) },
                    { 7, null, 7, new DateTime(2026, 5, 20, 13, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, null, 8, new DateTime(2026, 5, 20, 13, 10, 0, 0, DateTimeKind.Utc) },
                    { 9, null, 9, new DateTime(2026, 5, 20, 13, 20, 0, 0, DateTimeKind.Utc) },
                    { 10, null, 10, new DateTime(2026, 5, 20, 13, 30, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "ClinicalRecords",
                columns: new[] { "Id", "ChestPainType", "Cholesterol", "DoctorId", "ExerciseAngina", "FastingBS", "IsDeleted", "MajorVessels", "MaxHeartRate", "OldPeak", "PatientId", "RestECG", "RestingBP", "STSlope", "ThalResult", "VisitDate" },
                values: new object[,]
                {
                    { 1, (byte)3, (short)233, 1, (byte)0, (byte)1, false, (byte)0, (short)150, 2.3m, 1, (byte)0, (short)145, (byte)0, (byte)1, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, (byte)2, (short)250, 2, (byte)0, (byte)0, false, (byte)0, (short)187, 3.5m, 2, (byte)1, (short)130, (byte)0, (byte)2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, (byte)1, (short)204, 3, (byte)0, (byte)0, false, (byte)0, (short)172, 1.4m, 3, (byte)0, (short)130, (byte)2, (byte)2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, (byte)0, (short)354, 1, (byte)1, (byte)0, false, (byte)0, (short)163, 0.6m, 4, (byte)1, (short)120, (byte)2, (byte)2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, (byte)0, (short)203, 2, (byte)1, (byte)1, false, (byte)0, (short)155, 3.1m, 5, (byte)0, (short)140, (byte)0, (byte)3, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, (byte)2, (short)294, 3, (byte)0, (byte)1, false, (byte)3, (short)106, 1.9m, 6, (byte)1, (short)138, (byte)1, (byte)2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, (byte)0, (short)288, 4, (byte)1, (byte)1, false, (byte)2, (short)133, 4.0m, 7, (byte)0, (short)160, (byte)0, (byte)3, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, (byte)1, (short)190, 5, (byte)0, (byte)0, false, (byte)0, (short)180, 0.0m, 8, (byte)0, (short)110, (byte)2, (byte)2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, (byte)2, (short)210, 1, (byte)0, (byte)0, false, (byte)1, (short)160, 1.2m, 9, (byte)1, (short)125, (byte)1, (byte)2, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, (byte)3, (short)240, 2, (byte)1, (byte)1, false, (byte)2, (short)140, 2.5m, 10, (byte)0, (short)150, (byte)1, (byte)3, new DateTime(2026, 5, 20, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "DailyVitalLogs",
                columns: new[] { "Id", "ChestPainLevel", "DiastolicBP", "HasExerciseAngina", "HeartRate", "IsDeleted", "LoggedAt", "PatientId", "SystolicBP" },
                values: new object[,]
                {
                    { 1, (byte)2, (short)95, true, (short)95, false, new DateTime(2026, 5, 20, 1, 0, 0, 0, DateTimeKind.Utc), 1, (short)158 },
                    { 2, (byte)0, (short)80, false, (short)70, false, new DateTime(2026, 5, 20, 2, 0, 0, 0, DateTimeKind.Utc), 2, (short)122 },
                    { 3, (byte)1, (short)85, false, (short)80, false, new DateTime(2026, 5, 20, 3, 0, 0, 0, DateTimeKind.Utc), 3, (short)135 },
                    { 4, (byte)0, (short)75, false, (short)65, false, new DateTime(2026, 5, 20, 4, 0, 0, 0, DateTimeKind.Utc), 4, (short)118 },
                    { 5, (byte)1, (short)90, false, (short)88, false, new DateTime(2026, 5, 20, 5, 0, 0, 0, DateTimeKind.Utc), 5, (short)140 },
                    { 6, (byte)0, (short)82, false, (short)72, false, new DateTime(2026, 5, 20, 6, 0, 0, 0, DateTimeKind.Utc), 6, (short)125 },
                    { 7, (byte)3, (short)105, true, (short)110, false, new DateTime(2026, 5, 20, 7, 0, 0, 0, DateTimeKind.Utc), 7, (short)180 },
                    { 8, (byte)0, (short)70, false, (short)60, false, new DateTime(2026, 5, 20, 8, 0, 0, 0, DateTimeKind.Utc), 8, (short)110 },
                    { 9, (byte)1, (short)85, false, (short)78, false, new DateTime(2026, 5, 20, 9, 0, 0, 0, DateTimeKind.Utc), 9, (short)130 },
                    { 10, (byte)2, (short)98, true, (short)100, false, new DateTime(2026, 5, 20, 10, 0, 0, 0, DateTimeKind.Utc), 10, (short)165 }
                });

            migrationBuilder.InsertData(
                table: "AIRiskPredictions",
                columns: new[] { "Id", "ClinicalRecordId", "DailyLogId", "IsDeleted", "ModelVersion", "PatientId", "PredictedAt", "PredictedTarget", "RiskLevel", "RiskScore" },
                values: new object[,]
                {
                    { 1, null, 1, false, "v1.0", 1, new DateTime(2026, 5, 20, 1, 5, 0, 0, DateTimeKind.Utc), (byte)1, (byte)2, 0.78m },
                    { 2, null, 2, false, "v1.0", 2, new DateTime(2026, 5, 20, 2, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)0, 0.12m },
                    { 3, null, 3, false, "v1.0", 3, new DateTime(2026, 5, 20, 3, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)1, 0.35m },
                    { 4, null, 4, false, "v1.0", 4, new DateTime(2026, 5, 20, 4, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)0, 0.05m },
                    { 5, null, 5, false, "v1.0", 5, new DateTime(2026, 5, 20, 5, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)1, 0.45m },
                    { 6, null, 6, false, "v1.0", 6, new DateTime(2026, 5, 20, 6, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)0, 0.20m },
                    { 7, null, 7, false, "v1.0", 7, new DateTime(2026, 5, 20, 7, 5, 0, 0, DateTimeKind.Utc), (byte)1, (byte)2, 0.95m },
                    { 8, null, 8, false, "v1.0", 8, new DateTime(2026, 5, 20, 8, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)0, 0.08m },
                    { 9, null, 9, false, "v1.0", 9, new DateTime(2026, 5, 20, 9, 5, 0, 0, DateTimeKind.Utc), (byte)0, (byte)0, 0.25m },
                    { 10, null, 10, false, "v1.0", 10, new DateTime(2026, 5, 20, 10, 5, 0, 0, DateTimeKind.Utc), (byte)1, (byte)2, 0.82m }
                });

            migrationBuilder.InsertData(
                table: "ChatMessages",
                columns: new[] { "Id", "Content", "SenderRole", "SentAt", "SessionId" },
                values: new object[,]
                {
                    { 1, "Chào bác sĩ AI, tôi thấy hơi mệt", (byte)0, new DateTime(2026, 5, 20, 12, 1, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, "Hôm nay tôi đo huyết áp bình thường", (byte)0, new DateTime(2026, 5, 20, 12, 11, 0, 0, DateTimeKind.Utc), 2 },
                    { 3, "Tôi cần tư vấn chế độ ăn", (byte)0, new DateTime(2026, 5, 20, 12, 21, 0, 0, DateTimeKind.Utc), 3 },
                    { 4, "Thuốc hôm nay uống lúc mấy giờ?", (byte)0, new DateTime(2026, 5, 20, 12, 31, 0, 0, DateTimeKind.Utc), 4 },
                    { 5, "Tôi ngủ dậy hơi chóng mặt", (byte)0, new DateTime(2026, 5, 20, 12, 41, 0, 0, DateTimeKind.Utc), 5 },
                    { 6, "Hôm nay tôi đã chạy bộ 30 phút", (byte)0, new DateTime(2026, 5, 20, 12, 51, 0, 0, DateTimeKind.Utc), 6 },
                    { 7, "Cứu với, tôi bị nhói ngực quá!", (byte)0, new DateTime(2026, 5, 20, 13, 1, 0, 0, DateTimeKind.Utc), 7 },
                    { 8, "Cảm ơn AI đã tư vấn", (byte)0, new DateTime(2026, 5, 20, 13, 11, 0, 0, DateTimeKind.Utc), 8 },
                    { 9, "Nhịp tim 78 là ổn chưa?", (byte)0, new DateTime(2026, 5, 20, 13, 21, 0, 0, DateTimeKind.Utc), 9 },
                    { 10, "Ngực tôi hơi nặng nề", (byte)0, new DateTime(2026, 5, 20, 13, 31, 0, 0, DateTimeKind.Utc), 10 }
                });

            migrationBuilder.InsertData(
                table: "WarningAlerts",
                columns: new[] { "Id", "ClaimedAt", "ClaimedByDoctorId", "FlaggedAt", "IsDeleted", "PatientId", "PredictionId", "ResolutionNote", "Status" },
                values: new object[,]
                {
                    { 1, null, null, new DateTime(2026, 5, 20, 1, 6, 0, 0, DateTimeKind.Utc), false, 1, 1, null, (byte)0 },
                    { 2, null, 1, new DateTime(2026, 5, 20, 2, 6, 0, 0, DateTimeKind.Utc), false, 2, 2, null, (byte)1 },
                    { 3, null, null, new DateTime(2026, 5, 20, 3, 6, 0, 0, DateTimeKind.Utc), false, 3, 3, null, (byte)0 },
                    { 4, null, 2, new DateTime(2026, 5, 20, 4, 6, 0, 0, DateTimeKind.Utc), false, 4, 4, "Bệnh nhân ổn định, đã uống thuốc.", (byte)2 },
                    { 5, null, null, new DateTime(2026, 5, 20, 5, 6, 0, 0, DateTimeKind.Utc), false, 5, 5, null, (byte)0 },
                    { 6, null, 3, new DateTime(2026, 5, 20, 6, 6, 0, 0, DateTimeKind.Utc), false, 6, 6, null, (byte)1 },
                    { 7, null, null, new DateTime(2026, 5, 20, 7, 6, 0, 0, DateTimeKind.Utc), false, 7, 7, null, (byte)0 },
                    { 8, null, 4, new DateTime(2026, 5, 20, 8, 6, 0, 0, DateTimeKind.Utc), false, 8, 8, "Cảnh báo nhầm.", (byte)2 },
                    { 9, null, null, new DateTime(2026, 5, 20, 9, 6, 0, 0, DateTimeKind.Utc), false, 9, 9, null, (byte)0 },
                    { 10, null, 1, new DateTime(2026, 5, 20, 10, 6, 0, 0, DateTimeKind.Utc), false, 10, 10, null, (byte)1 }
                });

            migrationBuilder.InsertData(
                table: "EmailNotifications",
                columns: new[] { "Id", "AlertId", "Body", "CreatedAt", "PatientId", "Status", "Subject" },
                values: new object[,]
                {
                    { 1, 1, "Vui lòng liên hệ bác sĩ ngay", new DateTime(2026, 5, 20, 1, 7, 0, 0, DateTimeKind.Utc), 1, (byte)0, "CẢNH BÁO: Huyết áp bất thường" },
                    { 2, 2, "Chỉ số của bạn ổn định", new DateTime(2026, 5, 20, 2, 7, 0, 0, DateTimeKind.Utc), 2, (byte)0, "Cập nhật sinh hiệu" },
                    { 3, 3, "Nhịp tim bình thường", new DateTime(2026, 5, 20, 3, 7, 0, 0, DateTimeKind.Utc), 3, (byte)0, "Theo dõi nhịp tim" },
                    { 4, 4, "Cảnh báo đã được giải quyết", new DateTime(2026, 5, 20, 4, 7, 0, 0, DateTimeKind.Utc), 4, (byte)0, "Báo cáo ổn định" },
                    { 5, 5, "Theo dõi thêm tại nhà", new DateTime(2026, 5, 20, 5, 7, 0, 0, DateTimeKind.Utc), 5, (byte)0, "CẢNH BÁO NHẸ: Huyết áp tăng" },
                    { 6, 6, "Bác sĩ đang xem xét", new DateTime(2026, 5, 20, 6, 7, 0, 0, DateTimeKind.Utc), 6, (byte)0, "Đã tiếp nhận hồ sơ" },
                    { 7, 7, "Gọi cấp cứu 115 lập tức!", new DateTime(2026, 5, 20, 7, 7, 0, 0, DateTimeKind.Utc), 7, (byte)0, "CẢNH BÁO KHẨN CẤP: Tiền tai biến" },
                    { 8, 8, "Cảnh báo nhầm", new DateTime(2026, 5, 20, 8, 7, 0, 0, DateTimeKind.Utc), 8, (byte)0, "Hệ thống tự động hủy cảnh báo" },
                    { 9, 9, "Vui lòng nhập sinh hiệu", new DateTime(2026, 5, 20, 9, 7, 0, 0, DateTimeKind.Utc), 9, (byte)0, "Kiểm tra định kỳ" },
                    { 10, 10, "Bác sĩ An đã tiếp nhận", new DateTime(2026, 5, 20, 10, 7, 0, 0, DateTimeKind.Utc), 10, (byte)0, "CẢNH BÁO: Huyết áp tâm thu cao" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIRiskPredictions_ClinicalRecordId",
                table: "AIRiskPredictions",
                column: "ClinicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AIRiskPredictions_DailyLogId",
                table: "AIRiskPredictions",
                column: "DailyLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AIRiskPredictions_PatientId",
                table: "AIRiskPredictions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_PatientId",
                table: "ChatbotSessions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId",
                table: "ChatMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_DoctorId",
                table: "ClinicalRecords",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_PatientId",
                table: "ClinicalRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyVitalLogs_PatientId",
                table: "DailyVitalLogs",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_Shift",
                table: "Doctors",
                column: "IsOnShift",
                filter: "([IsDeleted]=(0))");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UserId",
                table: "Doctors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_AlertId",
                table: "EmailNotifications",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_PatientId",
                table: "EmailNotifications",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId",
                table: "Patients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D1053469C9F0C2",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarningAlerts_ClaimedByDoctorId",
                table: "WarningAlerts",
                column: "ClaimedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_WarningAlerts_Dashboard",
                table: "WarningAlerts",
                columns: new[] { "Status", "FlaggedAt" },
                descending: new[] { false, true },
                filter: "([IsDeleted]=(0))");

            migrationBuilder.CreateIndex(
                name: "IX_WarningAlerts_PatientId",
                table: "WarningAlerts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "UQ__WarningA__BAE4C1A10BC9FF16",
                table: "WarningAlerts",
                column: "PredictionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "EmailNotifications");

            migrationBuilder.DropTable(
                name: "ChatbotSessions");

            migrationBuilder.DropTable(
                name: "WarningAlerts");

            migrationBuilder.DropTable(
                name: "AIRiskPredictions");

            migrationBuilder.DropTable(
                name: "ClinicalRecords");

            migrationBuilder.DropTable(
                name: "DailyVitalLogs");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
