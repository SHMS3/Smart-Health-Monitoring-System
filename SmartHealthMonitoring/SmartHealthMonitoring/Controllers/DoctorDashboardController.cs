using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "Doctor")]
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
                    .Where(p => !p.IsDeleted && !p.User.IsDeleted)
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