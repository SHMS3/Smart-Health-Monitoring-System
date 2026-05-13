using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class AppointmentSlot
{
    public Guid SlotId { get; set; }

    public Guid ScheduleId { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsBooked { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual DoctorSchedule Schedule { get; set; } = null!;
}
