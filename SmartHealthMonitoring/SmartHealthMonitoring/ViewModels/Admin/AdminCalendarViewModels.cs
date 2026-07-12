namespace SmartHealthMonitoring.ViewModels.Admin;

public class AdminCalendarPageViewModel
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public List<AdminCalendarDoctorItem> Doctors { get; set; } = new();
    public int TotalAppointments { get; set; }
    public int ConfirmedCount { get; set; }
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
    public int BlockedSlotCount { get; set; }
}

public class AdminCalendarDoctorItem
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = null!;
    public string Specialty { get; set; } = null!;
    public string? RoomNumber { get; set; }
    public string Color { get; set; } = "#0ea5e9";
    public bool IsOnShift { get; set; }
}

public class AdminCalendarEventDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Start { get; set; } = null!;
    public string End { get; set; } = null!;
    public string BackgroundColor { get; set; } = null!;
    public string BorderColor { get; set; } = null!;
    public string TextColor { get; set; } = "#ffffff";
    public AdminCalendarEventExtendedProps ExtendedProps { get; set; } = new();
}

public class AdminCalendarEventExtendedProps
{
    public int? AppointmentId { get; set; }
    public int? SlotId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = null!;
    public string Specialty { get; set; } = null!;
    public string? RoomNumber { get; set; }
    public string? PatientName { get; set; }
    public string Status { get; set; } = null!;
    public string StatusLabel { get; set; } = null!;
    public string? PatientNote { get; set; }
    public string EventKind { get; set; } = "appointment"; // appointment | blocked
}

/// <summary>ADM-02: Báo cáo No-show &amp; tỷ lệ huỷ theo bác sĩ.</summary>
public class AdminNoShowReportViewModel
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public int TotalAppointments { get; set; }
    public int NoShowCount { get; set; }
    public int CancelledByPatientCount { get; set; }
    public int CancelledByDoctorCount { get; set; }
    public int CompletedCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int OtherCount { get; set; }

    public double OverallNoShowRate { get; set; }
    public double OverallCancelRate { get; set; }

    /// <summary>Labels/values for clinic-wide pie (outcome buckets).</summary>
    public List<string> OutcomeLabels { get; set; } = new();
    public List<int> OutcomeValues { get; set; } = new();
    public List<string> OutcomeColors { get; set; } = new();

    public List<AdminDoctorNoShowStat> DoctorStats { get; set; } = new();
}

public class AdminDoctorNoShowStat
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = null!;
    public string Specialty { get; set; } = null!;
    public int Total { get; set; }
    public int NoShowCount { get; set; }
    public int CancelledByPatientCount { get; set; }
    public int CancelledByDoctorCount { get; set; }
    public int CompletedCount { get; set; }

    public double NoShowRate { get; set; }
    public double CancelRate { get; set; }
    public double CancelByPatientRate { get; set; }
    public double CancelByDoctorRate { get; set; }
}

/// <summary>ADM-03: Heatmap giờ cao điểm trong tuần.</summary>
public class AdminHeatmapViewModel
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Cột: T2…CN (index 0 = Monday).</summary>
    public List<string> DayLabels { get; set; } = new();

    /// <summary>Hàng: nhãn giờ (VD "08:00").</summary>
    public List<string> HourLabels { get; set; } = new();

    /// <summary>Counts[hourIndex][dayIndex] — dayIndex 0=Mon … 6=Sun.</summary>
    public int[][] Counts { get; set; } = Array.Empty<int[]>();

    public int MaxCount { get; set; }
    public int TotalBookings { get; set; }
    public string? PeakDayLabel { get; set; }
    public string? PeakHourLabel { get; set; }
    public int PeakCount { get; set; }
    public int SlotMinutes { get; set; } = 60;
}
