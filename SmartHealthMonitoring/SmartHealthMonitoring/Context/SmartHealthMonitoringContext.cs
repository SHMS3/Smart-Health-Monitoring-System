using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Context;

public partial class SmartHealthMonitoringContext : DbContext
{
    public SmartHealthMonitoringContext()
    {
    }

    public SmartHealthMonitoringContext(DbContextOptions<SmartHealthMonitoringContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiriskPrediction> AiriskPredictions { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<ChatbotSession> ChatbotSessions { get; set; }

    public virtual DbSet<ClinicalRecord> ClinicalRecords { get; set; }

    public virtual DbSet<DailyVitalLog> DailyVitalLogs { get; set; }

    public virtual DbSet<Doctor> Doctors { get; set; }

    public virtual DbSet<EmailNotification> EmailNotifications { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WarningAlert> WarningAlerts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Để trống hàm này, không hardcode chuỗi kết nối ở đây nữa
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiriskPrediction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AIRiskPr__3214EC076EE87583");

            entity.ToTable("AIRiskPredictions");

            entity.Property(e => e.ModelVersion)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("v1.0");
            entity.Property(e => e.PredictedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RiskScore).HasColumnType("decimal(5, 4)");

            entity.HasOne(d => d.ClinicalRecord).WithMany(p => p.AiriskPredictions)
                .HasForeignKey(d => d.ClinicalRecordId)
                .HasConstraintName("FK__AIRiskPre__Clini__6383C8BA");

            entity.HasOne(d => d.DailyLog).WithMany(p => p.AiriskPredictions)
                .HasForeignKey(d => d.DailyLogId)
                .HasConstraintName("FK__AIRiskPre__Daily__6477ECF3");

            entity.HasOne(d => d.Patient).WithMany(p => p.AiriskPredictions)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AIRiskPre__Patie__628FA481");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ChatMess__3214EC07DF8E26C2");

            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Session).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatMessa__Sessi__7F2BE32F");
        });

        modelBuilder.Entity<ChatbotSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ChatbotS__3214EC07FA91225A");

            entity.Property(e => e.StartedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Patient).WithMany(p => p.ChatbotSessions)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatbotSe__Patie__7B5B524B");
        });

        modelBuilder.Entity<ClinicalRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Clinical__3214EC07E439AAE9");

            entity.Property(e => e.FastingBs).HasColumnName("FastingBS");
            entity.Property(e => e.OldPeak).HasColumnType("decimal(4, 1)");
            entity.Property(e => e.RestEcg).HasColumnName("RestECG");
            entity.Property(e => e.RestingBp).HasColumnName("RestingBP");
            entity.Property(e => e.Stslope).HasColumnName("STSlope");
            entity.Property(e => e.VisitDate).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Doctor).WithMany(p => p.ClinicalRecords)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ClinicalR__Docto__48CFD27E");

            entity.HasOne(d => d.Patient).WithMany(p => p.ClinicalRecords)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ClinicalR__Patie__47DBAE45");
        });

        modelBuilder.Entity<DailyVitalLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DailyVit__3214EC075CD951CD");

            entity.Property(e => e.DiastolicBp).HasColumnName("DiastolicBP");
            entity.Property(e => e.LoggedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SystolicBp).HasColumnName("SystolicBP");

            entity.HasOne(d => d.Patient).WithMany(p => p.DailyVitalLogs)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DailyVita__Patie__5812160E");
        });

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Doctors__3214EC07A1A2F30A");

            entity.HasIndex(e => e.IsOnShift, "IX_Doctors_Shift").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Specialty)
                .HasMaxLength(100)
                .HasDefaultValue("Tim mạch");

            entity.HasOne(d => d.User).WithMany(p => p.Doctors)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Doctors__UserId__4222D4EF");
        });

        modelBuilder.Entity<EmailNotification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailNot__3214EC0730D50919");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Subject).HasMaxLength(200);

            entity.Property(e => e.ToEmail).HasMaxLength(150).IsUnicode(false).HasDefaultValue(string.Empty);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.IsSent).HasDefaultValue(false);
            entity.Property(e => e.Status).HasDefaultValue((byte)0);
            entity.Property(e => e.SentAt);

            entity.HasOne(d => d.Alert).WithMany(p => p.EmailNotifications)
                .HasForeignKey(d => d.AlertId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EmailNoti__Alert__75A278F5");

            entity.HasOne(d => d.Patient).WithMany(p => p.EmailNotifications)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EmailNoti__Patie__76969D2E");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Patients__3214EC07383EFB80");

            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.Patients)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Patients__UserId__3D5E1FD2");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC07EBF30E0D");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D1053469C9F0C2").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<WarningAlert>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WarningA__3214EC071EB1CBC8");

            entity.HasIndex(e => new { e.Status, e.FlaggedAt }, "IX_WarningAlerts_Dashboard")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.PredictionId, "UQ__WarningA__BAE4C1A10BC9FF16").IsUnique();

            entity.Property(e => e.FlaggedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ResolutionNote).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.ClaimedByDoctor).WithMany(p => p.WarningAlerts)
                .HasForeignKey(d => d.ClaimedByDoctorId)
                .HasConstraintName("FK__WarningAl__Claim__6FE99F9F");

            entity.HasOne(d => d.Patient).WithMany(p => p.WarningAlerts)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__WarningAl__Patie__6E01572D");

            entity.HasOne(d => d.Prediction).WithOne(p => p.WarningAlert)
                .HasForeignKey<WarningAlert>(d => d.PredictionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__WarningAl__Predi__6EF57B66");
        });

        // ==========================================
        // DATA SEEDING (CHUẨN KHỚP 100% VỚI DATABASE V4)
        // ==========================================
        DateTime baseDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        // 1. Users (10 Bác sĩ ID 1-10, 10 Bệnh nhân ID 11-20)
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Email = "dr.an@smarthealth.vn", PasswordHash = "hash123", FullName = "Nguyễn Văn An", Role = 1, CreatedAt = baseDate },
            new User { Id = 2, Email = "dr.binh@smarthealth.vn", PasswordHash = "hash123", FullName = "Trần Thị Bình", Role = 1, CreatedAt = baseDate },
            new User { Id = 3, Email = "dr.cuong@smarthealth.vn", PasswordHash = "hash123", FullName = "Phạm Minh Cường", Role = 1, CreatedAt = baseDate },
            new User { Id = 4, Email = "dr.dung@smarthealth.vn", PasswordHash = "hash123", FullName = "Lê Tuấn Dũng", Role = 1, CreatedAt = baseDate },
            new User { Id = 5, Email = "dr.anh@smarthealth.vn", PasswordHash = "hash123", FullName = "Hoàng Mai Anh", Role = 1, CreatedAt = baseDate },
            new User { Id = 6, Email = "dr.phuc@smarthealth.vn", PasswordHash = "hash123", FullName = "Đinh Văn Phúc", Role = 1, CreatedAt = baseDate },
            new User { Id = 7, Email = "dr.chau@smarthealth.vn", PasswordHash = "hash123", FullName = "Nguyễn Bảo Châu", Role = 1, CreatedAt = baseDate },
            new User { Id = 8, Email = "dr.khanh@smarthealth.vn", PasswordHash = "hash123", FullName = "Vũ Quốc Khánh", Role = 1, CreatedAt = baseDate },
            new User { Id = 9, Email = "dr.tuan@smarthealth.vn", PasswordHash = "hash123", FullName = "Bùi Anh Tuấn", Role = 1, CreatedAt = baseDate },
            new User { Id = 10, Email = "dr.lan@smarthealth.vn", PasswordHash = "hash123", FullName = "Lý Phương Lan", Role = 1, CreatedAt = baseDate },

            new User { Id = 11, Email = "patient.hoa@gmail.com", PasswordHash = "hash123", FullName = "Nguyễn Thị Hoa", Role = 0, CreatedAt = baseDate },
            new User { Id = 12, Email = "patient.minh@gmail.com", PasswordHash = "hash123", FullName = "Trần Đức Minh", Role = 0, CreatedAt = baseDate },
            new User { Id = 13, Email = "patient.nhung@gmail.com", PasswordHash = "hash123", FullName = "Đỗ Hồng Nhung", Role = 0, CreatedAt = baseDate },
            new User { Id = 14, Email = "patient.tam@gmail.com", PasswordHash = "hash123", FullName = "Bùi Văn Tâm", Role = 0, CreatedAt = baseDate },
            new User { Id = 15, Email = "patient.long@gmail.com", PasswordHash = "hash123", FullName = "Phạm Thành Long", Role = 0, CreatedAt = baseDate },
            new User { Id = 16, Email = "patient.thuy@gmail.com", PasswordHash = "hash123", FullName = "Lê Thanh Thủy", Role = 0, CreatedAt = baseDate },
            new User { Id = 17, Email = "patient.hai@gmail.com", PasswordHash = "hash123", FullName = "Đặng Quang Hải", Role = 0, CreatedAt = baseDate },
            new User { Id = 18, Email = "patient.yen@gmail.com", PasswordHash = "hash123", FullName = "Võ Hoàng Yến", Role = 0, CreatedAt = baseDate },
            new User { Id = 19, Email = "patient.phong@gmail.com", PasswordHash = "hash123", FullName = "Ngô Đình Phong", Role = 0, CreatedAt = baseDate },
            new User { Id = 20, Email = "patient.mai@gmail.com", PasswordHash = "hash123", FullName = "Đoàn Ngọc Mai", Role = 0, CreatedAt = baseDate }
        );

        // 2. Doctors 
        modelBuilder.Entity<Doctor>().HasData(
            new Doctor { Id = 1, UserId = 1, Specialty = "Tim mạch can thiệp", IsOnShift = true },
            new Doctor { Id = 2, UserId = 2, Specialty = "Nhịp học tim mạch", IsOnShift = false },
            new Doctor { Id = 3, UserId = 3, Specialty = "Nội tim mạch", IsOnShift = true },
            new Doctor { Id = 4, UserId = 4, Specialty = "Phẫu thuật tim", IsOnShift = false },
            new Doctor { Id = 5, UserId = 5, Specialty = "Nội tim mạch", IsOnShift = true },
            new Doctor { Id = 6, UserId = 6, Specialty = "Tim mạch nhi", IsOnShift = false },
            new Doctor { Id = 7, UserId = 7, Specialty = "Chẩn đoán hình ảnh", IsOnShift = true },
            new Doctor { Id = 8, UserId = 8, Specialty = "Nội tim mạch", IsOnShift = false },
            new Doctor { Id = 9, UserId = 9, Specialty = "Tim mạch can thiệp", IsOnShift = true },
            new Doctor { Id = 10, UserId = 10, Specialty = "Nội tim mạch", IsOnShift = false }
        );

        // 3. Patients (Đã thêm DateOfBirth và Sex)
        modelBuilder.Entity<Patient>().HasData(
             new Patient { Id = 1, UserId = 11, DateOfBirth = new DateOnly(1965, 4, 12), Sex = 0, Phone = "0912345671" },
             new Patient { Id = 2, UserId = 12, DateOfBirth = new DateOnly(1978, 8, 22), Sex = 1, Phone = "0912345672" },
             new Patient { Id = 3, UserId = 13, DateOfBirth = new DateOnly(1955, 11, 5), Sex = 0, Phone = "0912345673" },
             new Patient { Id = 4, UserId = 14, DateOfBirth = new DateOnly(1982, 2, 17), Sex = 1, Phone = "0912345674" },
             new Patient { Id = 5, UserId = 15, DateOfBirth = new DateOnly(1960, 7, 30), Sex = 1, Phone = "0912345675" },
             new Patient { Id = 6, UserId = 16, DateOfBirth = new DateOnly(1990, 9, 14), Sex = 0, Phone = "0912345676" },
             new Patient { Id = 7, UserId = 17, DateOfBirth = new DateOnly(1950, 3, 25), Sex = 1, Phone = "0912345677" },
             new Patient { Id = 8, UserId = 18, DateOfBirth = new DateOnly(1975, 1, 10), Sex = 0, Phone = "0912345678" },
             new Patient { Id = 9, UserId = 19, DateOfBirth = new DateOnly(1988, 6, 5), Sex = 1, Phone = "0912345679" },
             new Patient { Id = 10, UserId = 20, DateOfBirth = new DateOnly(1962, 12, 12), Sex = 0, Phone = "0912345680" }
         );

        // 4. ClinicalRecords (Đã đổi tên thuộc tính chuẩn xác)
        modelBuilder.Entity<ClinicalRecord>().HasData(
            new ClinicalRecord { Id = 1, PatientId = 1, DoctorId = 1, ChestPainType = 3, RestingBp = 145, Cholesterol = 233, FastingBs = 1, RestEcg = 0, MaxHeartRate = 150, ExerciseAngina = 0, OldPeak = 2.3m, Stslope = 0, MajorVessels = 0, ThalResult = 1, VisitDate = baseDate },
            new ClinicalRecord { Id = 2, PatientId = 2, DoctorId = 2, ChestPainType = 2, RestingBp = 130, Cholesterol = 250, FastingBs = 0, RestEcg = 1, MaxHeartRate = 187, ExerciseAngina = 0, OldPeak = 3.5m, Stslope = 0, MajorVessels = 0, ThalResult = 2, VisitDate = baseDate },
            new ClinicalRecord { Id = 3, PatientId = 3, DoctorId = 3, ChestPainType = 1, RestingBp = 130, Cholesterol = 204, FastingBs = 0, RestEcg = 0, MaxHeartRate = 172, ExerciseAngina = 0, OldPeak = 1.4m, Stslope = 2, MajorVessels = 0, ThalResult = 2, VisitDate = baseDate },
            new ClinicalRecord { Id = 4, PatientId = 4, DoctorId = 1, ChestPainType = 0, RestingBp = 120, Cholesterol = 354, FastingBs = 0, RestEcg = 1, MaxHeartRate = 163, ExerciseAngina = 1, OldPeak = 0.6m, Stslope = 2, MajorVessels = 0, ThalResult = 2, VisitDate = baseDate },
            new ClinicalRecord { Id = 5, PatientId = 5, DoctorId = 2, ChestPainType = 0, RestingBp = 140, Cholesterol = 203, FastingBs = 1, RestEcg = 0, MaxHeartRate = 155, ExerciseAngina = 1, OldPeak = 3.1m, Stslope = 0, MajorVessels = 0, ThalResult = 3, VisitDate = baseDate },
            new ClinicalRecord { Id = 6, PatientId = 6, DoctorId = 3, ChestPainType = 2, RestingBp = 138, Cholesterol = 294, FastingBs = 1, RestEcg = 1, MaxHeartRate = 106, ExerciseAngina = 0, OldPeak = 1.9m, Stslope = 1, MajorVessels = 3, ThalResult = 2, VisitDate = baseDate },
            new ClinicalRecord { Id = 7, PatientId = 7, DoctorId = 4, ChestPainType = 0, RestingBp = 160, Cholesterol = 288, FastingBs = 1, RestEcg = 0, MaxHeartRate = 133, ExerciseAngina = 1, OldPeak = 4.0m, Stslope = 0, MajorVessels = 2, ThalResult = 3, VisitDate = baseDate },
            new ClinicalRecord { Id = 8, PatientId = 8, DoctorId = 5, ChestPainType = 1, RestingBp = 110, Cholesterol = 190, FastingBs = 0, RestEcg = 0, MaxHeartRate = 180, ExerciseAngina = 0, OldPeak = 0.0m, Stslope = 2, MajorVessels = 0, ThalResult = 2, VisitDate = baseDate },
            new ClinicalRecord { Id = 9, PatientId = 9, DoctorId = 1, ChestPainType = 2, RestingBp = 125, Cholesterol = 210, FastingBs = 0, RestEcg = 1, MaxHeartRate = 160, ExerciseAngina = 0, OldPeak = 1.2m, Stslope = 1, MajorVessels = 1, ThalResult = 2, VisitDate = baseDate },
            new ClinicalRecord { Id = 10, PatientId = 10, DoctorId = 2, ChestPainType = 3, RestingBp = 150, Cholesterol = 240, FastingBs = 1, RestEcg = 0, MaxHeartRate = 140, ExerciseAngina = 1, OldPeak = 2.5m, Stslope = 1, MajorVessels = 2, ThalResult = 3, VisitDate = baseDate }
        );

        // 5. DailyVitalLogs (Bổ sung HeartRate, ChestPainLevel, HasExerciseAngina)
        modelBuilder.Entity<DailyVitalLog>().HasData(
            new DailyVitalLog { Id = 1, PatientId = 1, SystolicBp = 158, DiastolicBp = 95, HeartRate = 95, ChestPainLevel = 2, HasExerciseAngina = true, LoggedAt = baseDate.AddHours(1) },
            new DailyVitalLog { Id = 2, PatientId = 2, SystolicBp = 122, DiastolicBp = 80, HeartRate = 70, ChestPainLevel = 0, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(2) },
            new DailyVitalLog { Id = 3, PatientId = 3, SystolicBp = 135, DiastolicBp = 85, HeartRate = 80, ChestPainLevel = 1, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(3) },
            new DailyVitalLog { Id = 4, PatientId = 4, SystolicBp = 118, DiastolicBp = 75, HeartRate = 65, ChestPainLevel = 0, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(4) },
            new DailyVitalLog { Id = 5, PatientId = 5, SystolicBp = 140, DiastolicBp = 90, HeartRate = 88, ChestPainLevel = 1, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(5) },
            new DailyVitalLog { Id = 6, PatientId = 6, SystolicBp = 125, DiastolicBp = 82, HeartRate = 72, ChestPainLevel = 0, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(6) },
            new DailyVitalLog { Id = 7, PatientId = 7, SystolicBp = 180, DiastolicBp = 105, HeartRate = 110, ChestPainLevel = 3, HasExerciseAngina = true, LoggedAt = baseDate.AddHours(7) },
            new DailyVitalLog { Id = 8, PatientId = 8, SystolicBp = 110, DiastolicBp = 70, HeartRate = 60, ChestPainLevel = 0, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(8) },
            new DailyVitalLog { Id = 9, PatientId = 9, SystolicBp = 130, DiastolicBp = 85, HeartRate = 78, ChestPainLevel = 1, HasExerciseAngina = false, LoggedAt = baseDate.AddHours(9) },
            new DailyVitalLog { Id = 10, PatientId = 10, SystolicBp = 165, DiastolicBp = 98, HeartRate = 100, ChestPainLevel = 2, HasExerciseAngina = true, LoggedAt = baseDate.AddHours(10) }
        );

        // 6. AIRiskPredictions (Thêm PredictedTarget và RiskLevel theo DB)
        modelBuilder.Entity<AiriskPrediction>().HasData(
            new AiriskPrediction { Id = 1, PatientId = 1, DailyLogId = 1, RiskScore = 0.78m, PredictedTarget = 1, RiskLevel = 2, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(1).AddMinutes(5) },
            new AiriskPrediction { Id = 2, PatientId = 2, DailyLogId = 2, RiskScore = 0.12m, PredictedTarget = 0, RiskLevel = 0, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(2).AddMinutes(5) },
            new AiriskPrediction { Id = 3, PatientId = 3, DailyLogId = 3, RiskScore = 0.35m, PredictedTarget = 0, RiskLevel = 1, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(3).AddMinutes(5) },
            new AiriskPrediction { Id = 4, PatientId = 4, DailyLogId = 4, RiskScore = 0.05m, PredictedTarget = 0, RiskLevel = 0, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(4).AddMinutes(5) },
            new AiriskPrediction { Id = 5, PatientId = 5, DailyLogId = 5, RiskScore = 0.45m, PredictedTarget = 0, RiskLevel = 1, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(5).AddMinutes(5) },
            new AiriskPrediction { Id = 6, PatientId = 6, DailyLogId = 6, RiskScore = 0.20m, PredictedTarget = 0, RiskLevel = 0, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(6).AddMinutes(5) },
            new AiriskPrediction { Id = 7, PatientId = 7, DailyLogId = 7, RiskScore = 0.95m, PredictedTarget = 1, RiskLevel = 2, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(7).AddMinutes(5) },
            new AiriskPrediction { Id = 8, PatientId = 8, DailyLogId = 8, RiskScore = 0.08m, PredictedTarget = 0, RiskLevel = 0, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(8).AddMinutes(5) },
            new AiriskPrediction { Id = 9, PatientId = 9, DailyLogId = 9, RiskScore = 0.25m, PredictedTarget = 0, RiskLevel = 0, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(9).AddMinutes(5) },
            new AiriskPrediction { Id = 10, PatientId = 10, DailyLogId = 10, RiskScore = 0.82m, PredictedTarget = 1, RiskLevel = 2, ModelVersion = "v1.0", PredictedAt = baseDate.AddHours(10).AddMinutes(5) }
        );

        // 7. WarningAlerts
        modelBuilder.Entity<WarningAlert>().HasData(
            new WarningAlert { Id = 1, PredictionId = 1, PatientId = 1, Status = 0, FlaggedAt = baseDate.AddHours(1).AddMinutes(6) },
            new WarningAlert { Id = 2, PredictionId = 2, PatientId = 2, Status = 1, ClaimedByDoctorId = 1, FlaggedAt = baseDate.AddHours(2).AddMinutes(6) },
            new WarningAlert { Id = 3, PredictionId = 3, PatientId = 3, Status = 0, FlaggedAt = baseDate.AddHours(3).AddMinutes(6) },
            new WarningAlert { Id = 4, PredictionId = 4, PatientId = 4, Status = 2, ClaimedByDoctorId = 2, ResolutionNote = "Bệnh nhân ổn định, đã uống thuốc.", FlaggedAt = baseDate.AddHours(4).AddMinutes(6) },
            new WarningAlert { Id = 5, PredictionId = 5, PatientId = 5, Status = 0, FlaggedAt = baseDate.AddHours(5).AddMinutes(6) },
            new WarningAlert { Id = 6, PredictionId = 6, PatientId = 6, Status = 1, ClaimedByDoctorId = 3, FlaggedAt = baseDate.AddHours(6).AddMinutes(6) },
            new WarningAlert { Id = 7, PredictionId = 7, PatientId = 7, Status = 0, FlaggedAt = baseDate.AddHours(7).AddMinutes(6) },
            new WarningAlert { Id = 8, PredictionId = 8, PatientId = 8, Status = 2, ClaimedByDoctorId = 4, ResolutionNote = "Cảnh báo nhầm.", FlaggedAt = baseDate.AddHours(8).AddMinutes(6) },
            new WarningAlert { Id = 9, PredictionId = 9, PatientId = 9, Status = 0, FlaggedAt = baseDate.AddHours(9).AddMinutes(6) },
            new WarningAlert { Id = 10, PredictionId = 10, PatientId = 10, Status = 1, ClaimedByDoctorId = 1, FlaggedAt = baseDate.AddHours(10).AddMinutes(6) }
            
        );

        // 8. ChatbotSessions
        modelBuilder.Entity<ChatbotSession>().HasData(
            new ChatbotSession { Id = 1, PatientId = 1, StartedAt = baseDate.AddHours(12) },
            new ChatbotSession { Id = 2, PatientId = 2, StartedAt = baseDate.AddHours(12).AddMinutes(10) },
            new ChatbotSession { Id = 3, PatientId = 3, StartedAt = baseDate.AddHours(12).AddMinutes(20) },
            new ChatbotSession { Id = 4, PatientId = 4, StartedAt = baseDate.AddHours(12).AddMinutes(30) },
            new ChatbotSession { Id = 5, PatientId = 5, StartedAt = baseDate.AddHours(12).AddMinutes(40) },
            new ChatbotSession { Id = 6, PatientId = 6, StartedAt = baseDate.AddHours(12).AddMinutes(50) },
            new ChatbotSession { Id = 7, PatientId = 7, StartedAt = baseDate.AddHours(13) },
            new ChatbotSession { Id = 8, PatientId = 8, StartedAt = baseDate.AddHours(13).AddMinutes(10) },
            new ChatbotSession { Id = 9, PatientId = 9, StartedAt = baseDate.AddHours(13).AddMinutes(20) },
            new ChatbotSession { Id = 10, PatientId = 10, StartedAt = baseDate.AddHours(13).AddMinutes(30) }
        );

        // 9. ChatMessages
        modelBuilder.Entity<ChatMessage>().HasData(
            new ChatMessage { Id = 1, SessionId = 1, SenderRole = 0, Content = "Chào bác sĩ AI, tôi thấy hơi mệt", SentAt = baseDate.AddHours(12).AddMinutes(1) },
            new ChatMessage { Id = 2, SessionId = 2, SenderRole = 0, Content = "Hôm nay tôi đo huyết áp bình thường", SentAt = baseDate.AddHours(12).AddMinutes(11) },
            new ChatMessage { Id = 3, SessionId = 3, SenderRole = 0, Content = "Tôi cần tư vấn chế độ ăn", SentAt = baseDate.AddHours(12).AddMinutes(21) },
            new ChatMessage { Id = 4, SessionId = 4, SenderRole = 0, Content = "Thuốc hôm nay uống lúc mấy giờ?", SentAt = baseDate.AddHours(12).AddMinutes(31) },
            new ChatMessage { Id = 5, SessionId = 5, SenderRole = 0, Content = "Tôi ngủ dậy hơi chóng mặt", SentAt = baseDate.AddHours(12).AddMinutes(41) },
            new ChatMessage { Id = 6, SessionId = 6, SenderRole = 0, Content = "Hôm nay tôi đã chạy bộ 30 phút", SentAt = baseDate.AddHours(12).AddMinutes(51) },
            new ChatMessage { Id = 7, SessionId = 7, SenderRole = 0, Content = "Cứu với, tôi bị nhói ngực quá!", SentAt = baseDate.AddHours(13).AddMinutes(1) },
            new ChatMessage { Id = 8, SessionId = 8, SenderRole = 0, Content = "Cảm ơn AI đã tư vấn", SentAt = baseDate.AddHours(13).AddMinutes(11) },
            new ChatMessage { Id = 9, SessionId = 9, SenderRole = 0, Content = "Nhịp tim 78 là ổn chưa?", SentAt = baseDate.AddHours(13).AddMinutes(21) },
            new ChatMessage { Id = 10, SessionId = 10, SenderRole = 0, Content = "Ngực tôi hơi nặng nề", SentAt = baseDate.AddHours(13).AddMinutes(31) }
        );

        // 10. EmailNotifications
        modelBuilder.Entity<EmailNotification>().HasData(
            new EmailNotification { Id = 1, AlertId = 1, PatientId = 1, ToEmail = "patient.hoa@gmail.com", Subject = "CẢNH BÁO: Huyết áp bất thường", Body = "Vui lòng liên hệ bác sĩ ngay", IsSent = true, Status = 1, SentAt = baseDate.AddHours(1).AddMinutes(7), CreatedAt = baseDate.AddHours(1).AddMinutes(7) },
            new EmailNotification { Id = 2, AlertId = 2, PatientId = 2, ToEmail = "patient.minh@gmail.com", Subject = "Cập nhật sinh hiệu", Body = "Chỉ số của bạn ổn định", IsSent = true, Status = 1, SentAt = baseDate.AddHours(2).AddMinutes(7), CreatedAt = baseDate.AddHours(2).AddMinutes(7) },
            new EmailNotification { Id = 3, AlertId = 3, PatientId = 3, ToEmail = "patient.nhung@gmail.com", Subject = "Theo dõi nhịp tim", Body = "Nhịp tim bình thường", IsSent = true, Status = 1, SentAt = baseDate.AddHours(3).AddMinutes(7), CreatedAt = baseDate.AddHours(3).AddMinutes(7) },
            new EmailNotification { Id = 4, AlertId = 4, PatientId = 4, ToEmail = "patient.tam@gmail.com", Subject = "Báo cáo ổn định", Body = "Cảnh báo đã được giải quyết", IsSent = true, Status = 1, SentAt = baseDate.AddHours(4).AddMinutes(7), CreatedAt = baseDate.AddHours(4).AddMinutes(7) },
            new EmailNotification { Id = 5, AlertId = 5, PatientId = 5, ToEmail = "patient.long@gmail.com", Subject = "CẢNH BÁO NHẸ: Huyết áp tăng", Body = "Theo dõi thêm tại nhà", IsSent = false, Status = 0, SentAt = null, ErrorMessage = "SMTP timeout", CreatedAt = baseDate.AddHours(5).AddMinutes(7) },
            new EmailNotification { Id = 6, AlertId = 6, PatientId = 6, ToEmail = "patient.thuy@gmail.com", Subject = "Đã tiếp nhận hồ sơ", Body = "Bác sĩ đang xem xét", IsSent = true, Status = 1, SentAt = baseDate.AddHours(6).AddMinutes(7), CreatedAt = baseDate.AddHours(6).AddMinutes(7) },
            new EmailNotification { Id = 7, AlertId = 7, PatientId = 7, ToEmail = "patient.hai@gmail.com", Subject = "CẢNH BÁO KHẨN CẤP: Tiền tai biến", Body = "Gọi cấp cứu 115 lập tức!", IsSent = true, Status = 1, SentAt = baseDate.AddHours(7).AddMinutes(7), CreatedAt = baseDate.AddHours(7).AddMinutes(7) },
            new EmailNotification { Id = 8, AlertId = 8, PatientId = 8, ToEmail = "patient.yen@gmail.com", Subject = "Hệ thống tự động hủy cảnh báo", Body = "Cảnh báo nhầm", IsSent = false, Status = 0, SentAt = null, ErrorMessage = "Invalid address", CreatedAt = baseDate.AddHours(8).AddMinutes(7) },
            new EmailNotification { Id = 9, AlertId = 9, PatientId = 9, ToEmail = "patient.phong@gmail.com", Subject = "Kiểm tra định kỳ", Body = "Vui lòng nhập sinh hiệu", IsSent = true, Status = 1, SentAt = baseDate.AddHours(9).AddMinutes(7), CreatedAt = baseDate.AddHours(9).AddMinutes(7) },
            new EmailNotification { Id = 10, AlertId = 10, PatientId = 10, ToEmail = "patient.mai@gmail.com", Subject = "CẢNH BÁO: Huyết áp tâm thu cao", Body = "Bác sĩ An đã tiếp nhận", IsSent = true, Status = 1, SentAt = baseDate.AddHours(10).AddMinutes(7), CreatedAt = baseDate.AddHours(10).AddMinutes(7) }
        );

        // bật RowVersion để tránh 2 người sửa cùng lúc
        modelBuilder.Entity<WarningAlert>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
