using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class ClinicalRecord
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public DateTime VisitDate { get; set; }

    public byte? ChestPainType { get; set; }

    public short? RestingBp { get; set; }

    public short? Cholesterol { get; set; }

    public byte? FastingBs { get; set; }

    public byte? RestEcg { get; set; }

    public short? MaxHeartRate { get; set; }

    public byte? ExerciseAngina { get; set; }

    public decimal? OldPeak { get; set; }

    public byte? Stslope { get; set; }

    public byte? MajorVessels { get; set; }

    public byte? ThalResult { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsViewForPatient { get; set; } = true;

    public string? EcgImageUrl { get; set; }

    public string? AttachmentUrl { get; set; }

    public virtual ICollection<AiriskPrediction> AiriskPredictions { get; set; } = new List<AiriskPrediction>();

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;
}
