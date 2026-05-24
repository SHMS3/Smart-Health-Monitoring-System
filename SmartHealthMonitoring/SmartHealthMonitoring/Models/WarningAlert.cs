using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class WarningAlert
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int PredictionId { get; set; }

    public int? ClaimedByDoctorId { get; set; }

    public byte Status { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public DateTime FlaggedAt { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public string? ResolutionNote { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Doctor? ClaimedByDoctor { get; set; }

    public virtual ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();

    public virtual Patient Patient { get; set; } = null!;

    public virtual AiriskPrediction Prediction { get; set; } = null!;
}
