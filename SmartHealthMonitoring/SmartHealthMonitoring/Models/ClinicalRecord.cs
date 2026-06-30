using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class ClinicalRecord
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public DateTime VisitDate { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói BP/Tim mạch)</summary>
    public byte? ChestPainType { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói BP/Tim mạch)</summary>
    public short? RestingBp { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Huyết học)</summary>
    public short? Cholesterol { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Huyết học)</summary>
    public byte? FastingBs { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Điện tâm đồ)</summary>
    public byte? RestEcg { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói BP/Tim mạch)</summary>
    public short? MaxHeartRate { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói BP/Tim mạch)</summary>
    public byte? ExerciseAngina { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Điện tâm đồ)</summary>
    public decimal? OldPeak { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Điện tâm đồ)</summary>
    public byte? Stslope { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Điện tâm đồ)</summary>
    public byte? MajorVessels { get; set; }

    /// <summary>Nullable: chỉ lưu khi bác sĩ thực sự đo (gói Điện tâm đồ)</summary>
    public byte? ThalResult { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsViewForPatient { get; set; } = true;

    public string? EcgImageUrl { get; set; }

    public string? AttachmentUrl { get; set; }

    public virtual ICollection<AiriskPrediction> AiriskPredictions { get; set; } = new List<AiriskPrediction>();

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;
}
