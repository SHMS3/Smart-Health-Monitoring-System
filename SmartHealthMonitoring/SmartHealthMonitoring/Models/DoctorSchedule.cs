using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class DoctorSchedule
{
    public Guid ScheduleId { get; set; }

    public Guid DoctorId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string Shift { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<AppointmentSlot> AppointmentSlots { get; set; } = new List<AppointmentSlot>();

    public virtual Doctor Doctor { get; set; } = null!;
}
