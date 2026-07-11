using System;

namespace SmartHealthMonitoring.Models;

public partial class EmergencyContact
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}
