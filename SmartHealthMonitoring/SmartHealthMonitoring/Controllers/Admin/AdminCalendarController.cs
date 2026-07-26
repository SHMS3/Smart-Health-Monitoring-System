using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin;

/// <summary>
/// ADM-01/02/03: Calendar Dashboard, No-show report &amp; peak-hours heatmap.
/// </summary>
[Authorize(Roles = "2")]
public class AdminCalendarController : Controller
{
    private static readonly string[] DoctorPalette =
    {
        "#0ea5e9", "#10b981", "#f59e0b", "#8b5cf6",
        "#ef4444", "#06b6d4", "#ec4899", "#84cc16",
        "#6366f1", "#f97316", "#14b8a6", "#a855f7"
    };

    private readonly SmartHealthMonitoringContext _context;

    public AdminCalendarController(SmartHealthMonitoringContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? weekStart)
    {
        var monday = weekStart ?? GetMondayOfWeek(DateOnly.FromDateTime(DateTime.Today));
        // Normalize to Monday if client passed another day
        monday = GetMondayOfWeek(monday);
        var sunday = monday.AddDays(6);

        var doctors = await LoadDoctorsAsync();
        var rangeStart = monday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var rangeEnd = sunday.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

        var appointments = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
            .Where(a =>
                a.Slot.SlotStart >= rangeStart &&
                a.Slot.SlotStart <= rangeEnd &&
                a.Status != AppointmentStatus.CancelledByPatient &&
                a.Status != AppointmentStatus.CancelledByDoctor)
            .ToListAsync();

        var blockedCount = await _context.AppointmentSlots
            .AsNoTracking()
            .CountAsync(s =>
                s.SlotStart >= rangeStart &&
                s.SlotStart <= rangeEnd &&
                s.Status == AppointmentSlotStatus.Blocked);

        var vm = new AdminCalendarPageViewModel
        {
            WeekStart = monday,
            WeekEnd = sunday,
            Doctors = doctors,
            TotalAppointments = appointments.Count,
            ConfirmedCount = appointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            PendingCount = appointments.Count(a =>
                a.Status == AppointmentStatus.Pending ||
                a.Status == AppointmentStatus.CancellationPending),
            CompletedCount = appointments.Count(a =>
                a.Status == AppointmentStatus.Completed ||
                a.Status == AppointmentStatus.NoShow),
            BlockedSlotCount = blockedCount
        };

        return View(vm);
    }

    /// <summary>
    /// JSON events for FullCalendar (range = visible calendar window).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Events(DateTime start, DateTime end, int? doctorId)
    {
        var rangeStart = DateTime.SpecifyKind(start, DateTimeKind.Local);
        var rangeEnd = DateTime.SpecifyKind(end, DateTimeKind.Local);

        var doctors = await LoadDoctorsAsync();
        var doctorMap = doctors.ToDictionary(d => d.DoctorId);

        var appointmentsQuery = _context.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a =>
                a.Slot.SlotStart >= rangeStart &&
                a.Slot.SlotStart < rangeEnd &&
                a.Status != AppointmentStatus.CancelledByPatient &&
                a.Status != AppointmentStatus.CancelledByDoctor);

        if (doctorId.HasValue)
            appointmentsQuery = appointmentsQuery.Where(a => a.DoctorId == doctorId.Value);

        var appointments = await appointmentsQuery
            .OrderBy(a => a.Slot.SlotStart)
            .ToListAsync();

        // Mỗi bác sĩ / khung 30 phút chỉ 1 ca (tránh trùng do data lẻ)
        appointments = appointments
            .GroupBy(a => new { a.DoctorId, Slot = SnapToSlotStart(a.Slot.SlotStart) })
            .Select(g => g
                .OrderBy(a => StatusPriority(a.Status))
                .ThenByDescending(a => a.Id)
                .First())
            .OrderBy(a => a.Slot.SlotStart)
            .ToList();

        var events = new List<AdminCalendarEventDto>();

        foreach (var a in appointments)
        {
            var color = doctorMap.TryGetValue(a.DoctorId, out var doc)
                ? doc.Color
                : ColorForDoctor(a.DoctorId);

            var statusMeta = GetStatusMeta(a.Status);
            var patientName = a.Patient?.User?.FullName ?? "—";
            var doctorName = a.Doctor?.User?.FullName ?? $"BS #{a.DoctorId}";
            var displayStart = SnapToSlotStart(a.Slot.SlotStart);
            var displayEnd = displayStart.AddMinutes(30);

            events.Add(new AdminCalendarEventDto
            {
                Id = $"appt-{a.Id}",
                Title = $"{patientName} · {GetShortName(doctorName)}",
                Start = FormatLocal(displayStart),
                End = FormatLocal(displayEnd),
                BackgroundColor = statusMeta.UseMuted ? Soften(color) : color,
                BorderColor = color,
                TextColor = "#ffffff",
                ExtendedProps = new AdminCalendarEventExtendedProps
                {
                    AppointmentId = a.Id,
                    SlotId = a.SlotId,
                    DoctorId = a.DoctorId,
                    DoctorName = doctorName,
                    Specialty = a.Doctor?.Specialty ?? "",
                    RoomNumber = a.Doctor?.RoomNumber,
                    PatientName = patientName,
                    Status = a.Status.ToString(),
                    StatusLabel = statusMeta.Label,
                    PatientNote = a.PatientNote,
                    EventKind = "appointment"
                }
            });
        }

        // Blocked slots (no appointment)
        var blockedQuery = _context.AppointmentSlots
            .AsNoTracking()
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .Where(s =>
                s.SlotStart >= rangeStart &&
                s.SlotStart < rangeEnd &&
                s.Status == AppointmentSlotStatus.Blocked);

        if (doctorId.HasValue)
            blockedQuery = blockedQuery.Where(s => s.DoctorId == doctorId.Value);

        var blockedSlots = await blockedQuery.ToListAsync();

        blockedSlots = blockedSlots
            .GroupBy(s => new { s.DoctorId, Slot = SnapToSlotStart(s.SlotStart) })
            .Select(g => g.OrderByDescending(s => s.Id).First())
            .ToList();

        foreach (var s in blockedSlots)
        {
            var color = doctorMap.TryGetValue(s.DoctorId, out var doc)
                ? doc.Color
                : ColorForDoctor(s.DoctorId);
            var doctorName = s.Doctor?.User?.FullName ?? $"BS #{s.DoctorId}";
            var displayStart = SnapToSlotStart(s.SlotStart);
            var displayEnd = displayStart.AddMinutes(30);

            events.Add(new AdminCalendarEventDto
            {
                Id = $"block-{s.Id}",
                Title = $"Chặn · {GetShortName(doctorName)}",
                Start = FormatLocal(displayStart),
                End = FormatLocal(displayEnd),
                BackgroundColor = "#94a3b8",
                BorderColor = color,
                TextColor = "#ffffff",
                ExtendedProps = new AdminCalendarEventExtendedProps
                {
                    SlotId = s.Id,
                    DoctorId = s.DoctorId,
                    DoctorName = doctorName,
                    Specialty = s.Doctor?.Specialty ?? "",
                    RoomNumber = s.Doctor?.RoomNumber,
                    Status = "Blocked",
                    StatusLabel = "Đã chặn",
                    EventKind = "blocked"
                }
            });
        }

        return Json(events);
    }

    /// <summary>
    /// ADM-02: Báo cáo No-show &amp; tỷ lệ huỷ theo từng bác sĩ.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> NoShowReport(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromDate = from ?? today.AddDays(-30);
        var toDate = to ?? today;
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        var rangeStart = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var rangeEnd = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

        // Chỉ tính lịch đã vào quy trình (bỏ Pending chưa duyệt)
        var rows = await (
            from a in _context.Appointments.AsNoTracking()
            join s in _context.AppointmentSlots.AsNoTracking() on a.SlotId equals s.Id
            join d in _context.Doctors.AsNoTracking() on a.DoctorId equals d.Id
            join u in _context.Users.AsNoTracking() on d.UserId equals u.Id
            where s.SlotStart >= rangeStart
                  && s.SlotStart <= rangeEnd
                  && a.Status != AppointmentStatus.Pending
            select new
            {
                a.DoctorId,
                DoctorName = u.FullName,
                d.Specialty,
                a.Status
            }).ToListAsync();

        var doctorStats = rows
            .GroupBy(r => new { r.DoctorId, r.DoctorName, r.Specialty })
            .Select(g =>
            {
                int total = g.Count();
                int noShow = g.Count(x => x.Status == AppointmentStatus.NoShow);
                int cancelPatient = g.Count(x => x.Status == AppointmentStatus.CancelledByPatient);
                int cancelDoctor = g.Count(x => x.Status == AppointmentStatus.CancelledByDoctor);
                int completed = g.Count(x => x.Status == AppointmentStatus.Completed);
                int cancelTotal = cancelPatient + cancelDoctor;

                return new AdminDoctorNoShowStat
                {
                    DoctorId = g.Key.DoctorId,
                    DoctorName = g.Key.DoctorName,
                    Specialty = g.Key.Specialty,
                    Total = total,
                    NoShowCount = noShow,
                    CancelledByPatientCount = cancelPatient,
                    CancelledByDoctorCount = cancelDoctor,
                    CompletedCount = completed,
                    NoShowRate = Pct(noShow, total),
                    CancelRate = Pct(cancelTotal, total),
                    CancelByPatientRate = Pct(cancelPatient, total),
                    CancelByDoctorRate = Pct(cancelDoctor, total)
                };
            })
            .OrderByDescending(x => x.NoShowRate)
            .ThenByDescending(x => x.CancelRate)
            .ThenBy(x => x.DoctorName)
            .ToList();

        int grandTotal = rows.Count;
        int totalNoShow = rows.Count(r => r.Status == AppointmentStatus.NoShow);
        int totalCancelPatient = rows.Count(r => r.Status == AppointmentStatus.CancelledByPatient);
        int totalCancelDoctor = rows.Count(r => r.Status == AppointmentStatus.CancelledByDoctor);
        int totalCompleted = rows.Count(r => r.Status == AppointmentStatus.Completed);
        int totalConfirmed = rows.Count(r => r.Status == AppointmentStatus.Confirmed);
        int totalCancel = totalCancelPatient + totalCancelDoctor;
        int totalOther = grandTotal - totalNoShow - totalCancel - totalCompleted - totalConfirmed;

        var outcomeBuckets = new (string Label, int Value, string Color)[]
        {
            ("Không đến (No-show)", totalNoShow, "#ef4444"),
            ("BN huỷ", totalCancelPatient, "#f59e0b"),
            ("BS huỷ", totalCancelDoctor, "#f97316"),
            ("Hoàn thành", totalCompleted, "#10b981"),
            ("Đã xác nhận", totalConfirmed, "#0ea5e9"),
            ("Khác", Math.Max(0, totalOther), "#94a3b8")
        };

        var vm = new AdminNoShowReportViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalAppointments = grandTotal,
            NoShowCount = totalNoShow,
            CancelledByPatientCount = totalCancelPatient,
            CancelledByDoctorCount = totalCancelDoctor,
            CompletedCount = totalCompleted,
            ConfirmedCount = totalConfirmed,
            OtherCount = Math.Max(0, totalOther),
            OverallNoShowRate = Pct(totalNoShow, grandTotal),
            OverallCancelRate = Pct(totalCancel, grandTotal),
            OutcomeLabels = outcomeBuckets.Where(b => b.Value > 0).Select(b => b.Label).ToList(),
            OutcomeValues = outcomeBuckets.Where(b => b.Value > 0).Select(b => b.Value).ToList(),
            OutcomeColors = outcomeBuckets.Where(b => b.Value > 0).Select(b => b.Color).ToList(),
            DoctorStats = doctorStats
        };

        return View(vm);
    }

    /// <summary>
    /// ADM-03: Heatmap giờ cao điểm — mật độ đặt lịch theo ngày trong tuần × khung giờ.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Heatmap(DateOnly? from, DateOnly? to, int slotMinutes = 60)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromDate = from ?? today.AddDays(-28);
        var toDate = to ?? today;
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        // Chỉ hỗ trợ 30 hoặc 60 phút cho lưới gọn
        slotMinutes = slotMinutes == 30 ? 30 : 60;

        const int dayStartHour = 7;
        const int dayEndHour = 20; // exclusive upper bound for last slot start
        int slotsPerDay = (dayEndHour - dayStartHour) * 60 / slotMinutes;

        var dayLabels = new List<string> { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };
        var hourLabels = new List<string>();
        for (int i = 0; i < slotsPerDay; i++)
        {
            var mins = dayStartHour * 60 + i * slotMinutes;
            hourLabels.Add($"{mins / 60:00}:{mins % 60:00}");
        }

        var counts = new int[slotsPerDay][];
        for (int r = 0; r < slotsPerDay; r++)
            counts[r] = new int[7];

        var rangeStart = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var rangeEnd = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

        // Đếm mọi lần đặt lịch (kể cả huỷ/no-show) — phản ánh nhu cầu đặt
        var slotStarts = await (
            from a in _context.Appointments.AsNoTracking()
            join s in _context.AppointmentSlots.AsNoTracking() on a.SlotId equals s.Id
            where s.SlotStart >= rangeStart && s.SlotStart <= rangeEnd
            select s.SlotStart
        ).ToListAsync();

        foreach (var start in slotStarts)
        {
            // DayOfWeek: Sunday=0 … Saturday=6 → map Mon=0 … Sun=6
            int dayIndex = ((int)start.DayOfWeek + 6) % 7;
            int minutesFromMidnight = start.Hour * 60 + start.Minute;
            int rowStart = dayStartHour * 60;
            int rowEnd = dayEndHour * 60;
            if (minutesFromMidnight < rowStart || minutesFromMidnight >= rowEnd)
                continue;

            int row = (minutesFromMidnight - rowStart) / slotMinutes;
            if (row < 0 || row >= slotsPerDay) continue;
            counts[row][dayIndex]++;
        }

        int max = 0;
        int peakR = -1, peakC = -1;
        int total = 0;
        for (int r = 0; r < slotsPerDay; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                int v = counts[r][c];
                total += v;
                if (v > max)
                {
                    max = v;
                    peakR = r;
                    peakC = c;
                }
            }
        }

        var vm = new AdminHeatmapViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            DayLabels = dayLabels,
            HourLabels = hourLabels,
            Counts = counts,
            MaxCount = max,
            TotalBookings = total,
            SlotMinutes = slotMinutes,
            PeakCount = max,
            PeakDayLabel = peakC >= 0 ? dayLabels[peakC] : null,
            PeakHourLabel = peakR >= 0 ? hourLabels[peakR] : null
        };

        return View(vm);
    }

    private static double Pct(int part, int total)
        => total <= 0 ? 0 : Math.Round(part * 100.0 / total, 1);

    private async Task<List<AdminCalendarDoctorItem>> LoadDoctorsAsync()
    {
        var doctors = await (from d in _context.Doctors.AsNoTracking()
                             join u in _context.Users.AsNoTracking() on d.UserId equals u.Id
                             where !d.IsDeleted && !u.IsDeleted && u.Role == 1
                             orderby u.FullName
                             select new { d.Id, u.FullName, d.Specialty, d.RoomNumber, d.IsOnShift })
            .ToListAsync();

        return doctors.Select(d => new AdminCalendarDoctorItem
        {
            DoctorId = d.Id,
            FullName = d.FullName,
            Specialty = d.Specialty,
            RoomNumber = d.RoomNumber,
            IsOnShift = d.IsOnShift,
            Color = ColorForDoctor(d.Id)
        }).ToList();
    }

    private static string ColorForDoctor(int doctorId)
        => DoctorPalette[Math.Abs(doctorId) % DoctorPalette.Length];

    private static string Soften(string hex)
    {
        // Lighten by mixing toward white for completed/muted events
        if (hex.Length != 7 || hex[0] != '#') return hex;
        static int Mix(int c) => (c + 255) / 2;
        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return $"#{Mix(r):X2}{Mix(g):X2}{Mix(b):X2}";
    }

    private static (string Label, bool UseMuted) GetStatusMeta(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Confirmed => ("Đã xác nhận", false),
        AppointmentStatus.Pending => ("Chờ duyệt", false),
        AppointmentStatus.CancellationPending => ("Chờ duyệt hủy", false),
        AppointmentStatus.Completed => ("Hoàn thành", true),
        AppointmentStatus.NoShow => ("Không đến", true),
        _ => (status.ToString(), true)
    };

    /// <summary>Wall-clock ISO without Z — matches how slots are stored (local hours tagged Utc).</summary>
    private static string FormatLocal(DateTime dt)
        => dt.ToString("yyyy-MM-dd'T'HH:mm:ss");

    private static DateTime SnapToSlotStart(DateTime dt)
    {
        var minutes = dt.Hour * 60 + dt.Minute;
        var snapped = (minutes / 30) * 30;
        return new DateTime(dt.Year, dt.Month, dt.Day, snapped / 60, snapped % 60, 0, dt.Kind);
    }

    private static int StatusPriority(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Confirmed => 0,
        AppointmentStatus.Pending => 1,
        AppointmentStatus.CancellationPending => 2,
        AppointmentStatus.Completed => 3,
        AppointmentStatus.NoShow => 4,
        _ => 5
    };

    private static string GetShortName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? fullName : parts[^1];
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        int diff = ((int)date.DayOfWeek + 6) % 7; // Mon=0 … Sun=6
        return date.AddDays(-diff);
    }
}
