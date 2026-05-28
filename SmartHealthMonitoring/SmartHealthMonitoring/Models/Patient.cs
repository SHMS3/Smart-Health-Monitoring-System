using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Patient
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public byte Sex { get; set; }

    public string? Phone { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<AiriskPrediction> AiriskPredictions { get; set; } = new List<AiriskPrediction>();

    public virtual ICollection<ChatbotSession> ChatbotSessions { get; set; } = new List<ChatbotSession>();

    public virtual ICollection<ClinicalRecord> ClinicalRecords { get; set; } = new List<ClinicalRecord>();

    public virtual ICollection<DailyVitalLog> DailyVitalLogs { get; set; } = new List<DailyVitalLog>();

    public virtual ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WarningAlert> WarningAlerts { get; set; } = new List<WarningAlert>();
    public virtual PatientThreshold? PatientThreshold { get; set; }
}
