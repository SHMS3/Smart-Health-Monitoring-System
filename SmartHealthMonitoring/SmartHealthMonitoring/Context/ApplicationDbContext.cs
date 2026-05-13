using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Context;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiInsight> AiInsights { get; set; }

    public virtual DbSet<Alert> Alerts { get; set; }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AppointmentSlot> AppointmentSlots { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Doctor> Doctors { get; set; }

    public virtual DbSet<DoctorSchedule> DoctorSchedules { get; set; }

    public virtual DbSet<GlobalThreshold> GlobalThresholds { get; set; }

    public virtual DbSet<HealthMetric> HealthMetrics { get; set; }

    public virtual DbSet<LabResult> LabResults { get; set; }

    public virtual DbSet<MedicalRecord> MedicalRecords { get; set; }

    public virtual DbSet<MetricType> MetricTypes { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiInsight>(entity =>
        {
            entity.HasKey(e => e.InsightId).HasName("PK__AiInsigh__6A3F54F42AC19670");

            entity.Property(e => e.InsightId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ModelVersion).HasMaxLength(50);
            entity.Property(e => e.PredictedDisease).HasMaxLength(200);
            entity.Property(e => e.RiskPercentage).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Patient).WithMany(p => p.AiInsights)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AiInsights_Patients");
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.AlertId).HasName("PK__Alerts__EBB16A8D9D3D0B06");

            entity.HasIndex(e => e.PatientId, "IX_Alerts_PatientId");

            entity.Property(e => e.AlertId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Severity).HasMaxLength(20);

            entity.HasOne(d => d.Metric).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.MetricId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alerts_HealthMetrics");

            entity.HasOne(d => d.Patient).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alerts_Patients");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC242B8353B");

            entity.HasIndex(e => e.Status, "IX_Appointments_Status");

            entity.HasIndex(e => e.SlotId, "UQ__Appointm__0A124AAE1F24CEC2").IsUnique();

            entity.Property(e => e.AppointmentId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.SymptomsNote).HasMaxLength(500);

            entity.HasOne(d => d.Patient).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Patients");

            entity.HasOne(d => d.Slot).WithOne(p => p.Appointment)
                .HasForeignKey<Appointment>(d => d.SlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Slots");
        });

        modelBuilder.Entity<AppointmentSlot>(entity =>
        {
            entity.HasKey(e => e.SlotId).HasName("PK__Appointm__0A124AAF38799BA2");

            entity.Property(e => e.SlotId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Schedule).WithMany(p => p.AppointmentSlots)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentSlots_Schedules");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__AuditLog__5E548648307A41C9");

            entity.Property(e => e.LogId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.RecordId).HasMaxLength(100);
            entity.Property(e => e.TableName).HasMaxLength(100);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditLogs_Users");
        });

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasKey(e => e.DoctorId).HasName("PK__Doctors__2DC00EBF5811057E");

            entity.HasIndex(e => e.UserId, "UQ__Doctors__1788CC4DD1F1C5F0").IsUnique();

            entity.Property(e => e.DoctorId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.LicenseNumber).HasMaxLength(50);
            entity.Property(e => e.Specialty).HasMaxLength(100);

            entity.HasOne(d => d.User).WithOne(p => p.Doctor)
                .HasForeignKey<Doctor>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Doctors_Users");
        });

        modelBuilder.Entity<DoctorSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("PK__DoctorSc__9C8A5B49619E8FDC");

            entity.Property(e => e.ScheduleId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Shift).HasMaxLength(50);

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorSchedules)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DoctorSchedules_Doctors");
        });

        modelBuilder.Entity<GlobalThreshold>(entity =>
        {
            entity.HasKey(e => e.ThresholdId).HasName("PK__GlobalTh__8E87A7D03DD2C22C");

            entity.HasIndex(e => new { e.MetricTypeId, e.MinAge, e.MaxAge }, "UQ_GlobalThresholds").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SafeMaxValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SafeMinValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SeverityLevel).HasMaxLength(20);
            entity.Property(e => e.WarningMessage).HasMaxLength(500);

            entity.HasOne(d => d.MetricType).WithMany(p => p.GlobalThresholds)
                .HasForeignKey(d => d.MetricTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GlobalThresholds_MetricTypes");
        });

        modelBuilder.Entity<HealthMetric>(entity =>
        {
            entity.HasKey(e => e.MetricId).HasName("PK__HealthMe__561056A5226DE9F4");

            entity.HasIndex(e => e.MeasuredAt, "IX_HealthMetrics_MeasuredAt");

            entity.HasIndex(e => e.PatientId, "IX_HealthMetrics_PatientId");

            entity.Property(e => e.MetricId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.MeasuredAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Source)
                .HasMaxLength(50)
                .HasDefaultValue("Manual");
            entity.Property(e => e.Value).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MetricType).WithMany(p => p.HealthMetrics)
                .HasForeignKey(d => d.MetricTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HealthMetrics_MetricTypes");

            entity.HasOne(d => d.Patient).WithMany(p => p.HealthMetrics)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HealthMetrics_Patients");
        });

        modelBuilder.Entity<LabResult>(entity =>
        {
            entity.HasKey(e => e.LabId).HasName("PK__LabResul__EDBD68DADF86C556");

            entity.Property(e => e.LabId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.TestName).HasMaxLength(200);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Record).WithMany(p => p.LabResults)
                .HasForeignKey(d => d.RecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LabResults_Records");
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK__MedicalR__FBDF78E9225F4F28");

            entity.HasIndex(e => e.PatientId, "IX_MedicalRecords_PatientId");

            entity.Property(e => e.RecordId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.Appointment).WithMany(p => p.MedicalRecords)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_MedicalRecords_Appointments");

            entity.HasOne(d => d.Doctor).WithMany(p => p.MedicalRecords)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicalRecords_Doctors");

            entity.HasOne(d => d.Patient).WithMany(p => p.MedicalRecords)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicalRecords_Patients");
        });

        modelBuilder.Entity<MetricType>(entity =>
        {
            entity.HasKey(e => e.MetricTypeId).HasName("PK__MetricTy__79DBA064F17BDC53");

            entity.HasIndex(e => e.Code, "UQ__MetricTy__A25C5AA7F74B0D70").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12F881E2DE");

            entity.HasIndex(e => e.UserId, "IX_Notifications_UserId");

            entity.Property(e => e.NotificationId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.PatientId).HasName("PK__Patients__970EC366172A5CE9");

            entity.HasIndex(e => e.UserId, "UQ__Patients__1788CC4D2452BB1A").IsUnique();

            entity.Property(e => e.PatientId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.BloodType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EmergencyContact).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);

            entity.HasOne(d => d.User).WithOne(p => p.Patient)
                .HasForeignKey<Patient>(d => d.UserId)
                .HasConstraintName("FK_Patients_Users");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1ACEE3C051");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160C50DD254").IsUnique();

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C118BF1FE");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E432499C32").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534EAABED36").IsUnique();

            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Username).HasMaxLength(100);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
