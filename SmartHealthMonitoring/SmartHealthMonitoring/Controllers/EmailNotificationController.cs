using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")]
    public class EmailNotificationController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public EmailNotificationController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(byte? status, string? emailType, DateTime? fromDate, DateTime? toDate, string? keyword)
        {
            var query = _context.EmailNotifications
                .Include(e => e.Patient).ThenInclude(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(e => (e.Patient != null && e.Patient.User != null && e.Patient.User.FullName.Contains(keyword)) ||
                                         e.ToEmail.Contains(keyword));
            }

            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);

            if (fromDate.HasValue)
                query = query.Where(e => e.CreatedAt >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(e => e.CreatedAt < toDate.Value.Date.AddDays(1));

            if (!string.IsNullOrEmpty(emailType))
            {
                if (emailType == "Mời tái khám")
                    query = query.Where(e => e.Subject.Contains("Tái Khám") || e.Subject.Contains("Tái khám") || e.Subject.Contains("tái khám"));
                else if (emailType == "Cảnh báo sức khỏe")
                    query = query.Where(e => e.Subject.Contains("CẢNH BÁO") || e.Subject.Contains("Cảnh báo") || e.Subject.Contains("cảnh báo"));
            }

            var emailsList = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            // Load doctor names (SentByDoctorId → Doctor → User.FullName)
            var doctorIds = emailsList
                .Where(e => e.SentByDoctorId.HasValue)
                .Select(e => e.SentByDoctorId!.Value)
                .Distinct()
                .ToList();

            var doctorNames = await _context.Doctors
                .Include(d => d.User)
                .Where(d => doctorIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.User?.FullName ?? "Bác sĩ");

            var dtos = emailsList.Select(e => new EmailHistoryDto
            {
                Id = e.Id,
                PatientName = e.Patient?.User?.FullName ?? "Không rõ",
                ToEmail = e.ToEmail,
                Subject = e.Subject,
                Status = e.Status,
                StatusDisplay = GetStatusDisplay(e.Status),
                CreatedAt = e.CreatedAt,
                SentAt = e.SentAt,
                ErrorMessage = e.ErrorMessage,
                Body = e.Body,
                AlertId = e.AlertId > 0 ? e.AlertId : null,
                EmailType = GetEmailType(e.Subject),
                SenderName = e.SentByDoctorId.HasValue
                    ? (doctorNames.TryGetValue(e.SentByDoctorId.Value, out var name) ? name : "Bác sĩ")
                    : "Hệ thống tự động"
            }).ToList();

            // Stats: 7 ngày qua
            var since7Days = DateTime.Now.AddDays(-7);
            var statsAll = await _context.EmailNotifications
                .Where(e => e.CreatedAt >= since7Days)
                .ToListAsync();

            var stats = new EmailStats
            {
                TotalLast7Days = statsAll.Count,
                Succeeded = statsAll.Count(e => e.Status == 1),
                Failed = statsAll.Count(e => e.Status == 2),
                ByAI = statsAll.Count(e => e.SentByDoctorId == null),
                ByDoctor = statsAll.Count(e => e.SentByDoctorId != null)
            };

            var viewModel = new EmailHistoryIndexViewModel
            {
                Emails = dtos,
                FilterStatus = status,
                FilterEmailType = emailType,
                FromDate = fromDate,
                ToDate = toDate,
                Stats = stats
            };

            return View(viewModel);
        }

        private static string GetStatusDisplay(byte status) => status switch
        {
            0 => "Chờ gửi",
            1 => "Thành công",
            2 => "Thất bại",
            _ => "Không xác định"
        };

        private static string GetEmailType(string subject)
        {
            if (subject.Contains("Tái Khám", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("tái khám", StringComparison.OrdinalIgnoreCase))
                return "Mời tái khám";

            if (subject.Contains("CẢNH BÁO", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("cảnh báo", StringComparison.OrdinalIgnoreCase))
                return "Cảnh báo sức khỏe";

            return "Khác";
        }
    }
}
