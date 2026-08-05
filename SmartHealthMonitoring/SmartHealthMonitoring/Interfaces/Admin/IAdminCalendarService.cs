using SmartHealthMonitoring.ViewModels.Admin;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IAdminCalendarService
{
    Task<AdminCalendarPageViewModel> GetWeekSummaryAsync(DateOnly? weekStart);
    Task<List<AdminCalendarEventDto>> GetCalendarEventsAsync(DateTime start, DateTime end, int? doctorId);
    Task<AdminNoShowReportViewModel> GetNoShowReportAsync(DateOnly? from, DateOnly? to);
    Task<AdminHeatmapViewModel> GetHeatmapAsync(DateOnly? from, DateOnly? to, int slotMinutes);
}
