using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Doctor
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Specialty { get; set; } = null!;

    public bool IsOnShift { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<ClinicalRecord> ClinicalRecords { get; set; } = new List<ClinicalRecord>();

    public string? CitizenId { get; set; }

    public string? PracticeLicense { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public bool IsPhoneVerified { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public byte? Sex { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WarningAlert> WarningAlerts { get; set; } = new List<WarningAlert>();

    public virtual ICollection<DoctorWorkSchedule> WorkSchedules { get; set; } = new List<DoctorWorkSchedule>();

    public virtual ICollection<AppointmentSlot> AppointmentSlots { get; set; } = new List<AppointmentSlot>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
