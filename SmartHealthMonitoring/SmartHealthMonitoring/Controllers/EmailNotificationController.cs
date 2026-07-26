using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1,2")]
    public class EmailNotificationController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public EmailNotificationController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(byte? status, string? emailType, DateTime? fromDate, DateTime? toDate, string? keyword, int? patientId, string? sender, int page = 1)
        {
            const int pageSize = 10;
            var today = DateTime.Today;
            fromDate ??= today;
            toDate ??= today;
            page = Math.Max(page, 1);

            IQueryable<EmailNotification> accessibleEmails = _context.EmailNotifications
                .AsNoTracking();

            if (User.IsInRole("1"))
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim, out var userId))
                    return Forbid();

                var doctorId = await _context.Doctors
                    .Where(d => d.UserId == userId && !d.IsDeleted)
                    .Select(d => (int?)d.Id)
                    .FirstOrDefaultAsync();

                if (!doctorId.HasValue)
                    return Forbid();

                accessibleEmails = accessibleEmails.Where(e =>
                    e.SentByDoctorId == null ||
                    e.SentByDoctorId == doctorId.Value);
            }

            var query = accessibleEmails
                .Include(e => e.Patient).ThenInclude(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(e => (e.Patient != null && e.Patient.User != null && e.Patient.User.FullName.Contains(keyword)) ||
                                         e.ToEmail.Contains(keyword));
            }

            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);

            if (patientId.HasValue)
                query = query.Where(e => e.PatientId == patientId.Value);

            if (!string.IsNullOrWhiteSpace(sender))
            {
                if (sender == "system")
                {
                    query = query.Where(e => e.SentByDoctorId == null);
                }
                else if (sender.StartsWith("doctor:", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(sender.Substring("doctor:".Length), out var senderDoctorId))
                {
                    query = query.Where(e => e.SentByDoctorId == senderDoctorId);
                }
            }

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
                else if (emailType == "Nhắc ghi chỉ số")
                    query = query.Where(e => e.Status == 3);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var emailsList = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                ErrorMessage = GetErrorDisplay(e.ErrorMessage),
                Body = e.Body,
                AlertId = e.AlertId > 0 ? e.AlertId : null,
                EmailType = e.Status == 3 ? "Nhắc ghi chỉ số" : GetEmailType(e.Subject),
                SenderName = e.SentByDoctorId.HasValue
                    ? (doctorNames.TryGetValue(e.SentByDoctorId.Value, out var name) ? name : "Bác sĩ")
                    : "Hệ thống tự động"
            }).ToList();

            // Stats: 7 ngày qua
            var since7Days = DateTime.Now.AddDays(-7);
            var statsAll = await accessibleEmails
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

            var patientOptions = await accessibleEmails
                .Select(e => new
                {
                    e.PatientId,
                    PatientName = e.Patient.User.FullName ?? e.ToEmail
                })
                .Distinct()
                .OrderBy(e => e.PatientName)
                .ToListAsync();

            var senderDoctorIds = await accessibleEmails
                .Where(e => e.SentByDoctorId.HasValue)
                .Select(e => e.SentByDoctorId!.Value)
                .Distinct()
                .ToListAsync();

            var senderDoctors = await _context.Doctors
                .Include(d => d.User)
                .Where(d => senderDoctorIds.Contains(d.Id))
                .OrderBy(d => d.User!.FullName)
                .Select(d => new
                {
                    d.Id,
                    DoctorName = d.User != null ? d.User.FullName : "Bác sĩ"
                })
                .ToListAsync();

            var hasSystemSender = await accessibleEmails
                .AnyAsync(e => e.SentByDoctorId == null);

            var viewModel = new EmailHistoryIndexViewModel
            {
                Emails = dtos,
                FilterStatus = status,
                FilterEmailType = emailType,
                FromDate = fromDate,
                ToDate = toDate,
                FilterKeyword = keyword,
                FilterPatientId = patientId,
                FilterSender = sender,
                PatientOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Bệnh nhân" }
                },
                SenderOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Người gửi" }
                },
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Stats = stats
            };

            viewModel.PatientOptions.AddRange(patientOptions.Select(p => new SelectListItem
            {
                Value = p.PatientId.ToString(),
                Text = p.PatientName
            }));

            if (hasSystemSender)
            {
                viewModel.SenderOptions.Add(new SelectListItem
                {
                    Value = "system",
                    Text = "Hệ thống tự động"
                });
            }

            viewModel.SenderOptions.AddRange(senderDoctors.Select(d => new SelectListItem
            {
                Value = $"doctor:{d.Id}",
                Text = d.DoctorName
            }));

            return View(viewModel);
        }

        private static string GetStatusDisplay(byte status) => status switch
        {
            0 => "Chờ gửi",
            1 => "Thành công",
            2 => "Thất bại",
            3 => "Thông báo nội bộ",
            _ => "Không xác định"
        };

        private static string? GetErrorDisplay(string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return null;

            return errorMessage.Contains("Daily user sending limit exceeded", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("5.4.5", StringComparison.OrdinalIgnoreCase)
                    ? "Tài khoản Gmail đã đạt giới hạn gửi email trong ngày. Vui lòng chờ Google khôi phục hạn mức hoặc đổi tài khoản gửi email."
                    : "Không thể gửi email. Vui lòng kiểm tra cấu hình gửi email hoặc thử lại sau.";
        }

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
