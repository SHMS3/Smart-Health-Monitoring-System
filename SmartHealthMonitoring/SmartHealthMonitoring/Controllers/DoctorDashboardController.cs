using Microsoft.AspNetCore.Authorization; // THÊM THƯ VIỆN NÀY
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")] // 1. THÊM DÒNG NÀY: Khóa cửa, chỉ cho Bác sĩ vào
    public class DoctorDashboardController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public DoctorDashboardController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                var patients = await _context.Patients
                    .Include(p => p.User)
                    // 2. THÊM ĐIỀU KIỆN p.User.Role == 0 VÀO ĐÂY: Chỉ lấy Bệnh nhân
                    .Where(p => !p.IsDeleted && !p.User.IsDeleted && p.User.Role == 0)
                    .Select(p => new PatientListViewModel
                    {
                        PatientId = p.Id,
                        FullName = p.User.FullName,
                        Age = today.Year - p.DateOfBirth.Year - (today.DayOfYear < p.DateOfBirth.DayOfYear ? 1 : 0),
                        SexDisplay = p.Sex == 1 ? "Nam" : "Nữ",
                        Phone = p.Phone ?? "N/A"
                    })
                    .ToListAsync();

                return View(patients);
            }
            catch (Exception ex)
            {
                // TODO: Log exception (Serilog/NLog)
                TempData["Error"] = "Lỗi khi tải danh sách bệnh nhân: " + ex.Message;
                return View(new List<PatientListViewModel>());
            }
        }
    }
}