using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    public class EmailNotificationController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public EmailNotificationController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(byte? status)
        {
            var query = _context.EmailNotifications
                .Include(e => e.Patient).ThenInclude(p => p.User)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(e => e.Status == status.Value);
            }

            var emailsList = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

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
                Body = e.Body
            }).ToList();

            var viewModel = new EmailHistoryIndexViewModel
            {
                Emails = dtos,
                FilterStatus = status
            };

            return View(viewModel);
        }

        private string GetStatusDisplay(byte status)
        {
            return status switch
            {
                0 => "Chờ gửi",
                1 => "Thành công",
                2 => "Thất bại",
                _ => "Không xác định"
            };
        }
    }
}
