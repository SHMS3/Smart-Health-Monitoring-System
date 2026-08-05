using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class DailyVitalLog
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public DateTime LoggedAt { get; set; }

    public short SystolicBp { get; set; }

    public short DiastolicBp { get; set; }

    public short HeartRate { get; set; }

    public byte ChestPainLevel { get; set; }

    public bool HasExerciseAngina { get; set; }

    public bool IsDeleted { get; set; }
    public byte UpdateCount { get; set; } = 0;

    public bool IsUpdateLocked { get; set; } = false;

    public virtual ICollection<AiriskPrediction> AiriskPredictions { get; set; } = new List<AiriskPrediction>();

    public virtual Patient Patient { get; set; } = null!;
}
