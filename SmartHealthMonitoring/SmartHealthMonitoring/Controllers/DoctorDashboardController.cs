using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")]
    public class DoctorDashboardController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public DoctorDashboardController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // 1. Dựng Query cơ sở
                var query = _context.Patients
                    .Include(p => p.User)
                    .Where(p => !p.IsDeleted && !p.User.IsDeleted && p.User.Role == 0);

                // 2. Đếm tổng số bệnh nhân thỏa mãn điều kiện
                int totalRecords = await query.CountAsync();

                // 3. Thực hiện phân trang và map dữ liệu sang ViewModel
                var items = await query
                    .OrderByDescending(p => p.User.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PatientListViewModel
                    {
                        PatientId = p.Id,
                        FullName = p.User.FullName,
                        Age = today.Year - p.DateOfBirth.Year - (today.DayOfYear < p.DateOfBirth.DayOfYear ? 1 : 0),
                        SexDisplay = p.Sex == 1 ? "Nam" : "Nữ",
                        Phone = p.Phone ?? "N/A"
                    })
                    .ToListAsync();

                // 4. Đóng gói kết quả
                var result = new PagedResult<PatientListViewModel>
                {
                    Items = items,
                    TotalCount = totalRecords,
                    Page = page,
                    PageSize = pageSize
                };

                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tải danh sách bệnh nhân: " + ex.Message;
                return View(new PagedResult<PatientListViewModel>());
            }
        }

        [HttpGet("DoctorDashboard/PatientProfile/{patientId}")]
        public async Task<IActionResult> PatientProfile(int patientId, int page = 1)
        {
            int pageSize = 5; // Hiển thị 5 kết quả AI mỗi trang cho gọn màn hình

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);

            if (patient == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin bệnh nhân.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Tách Query của AI ra để phân trang
            var aiQuery = _context.AiriskPredictions
                .Where(a => a.PatientId == patientId && !a.IsDeleted);

            int totalAiCount = await aiQuery.CountAsync();

            var pagedAiItems = await aiQuery
                .OrderByDescending(a => a.PredictedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 2. Gắn vào Model
            var model = new PatientProfileViewModel
            {
                Patient = patient,
                ClinicalRecords = await _context.ClinicalRecords
                    .Where(c => c.PatientId == patientId && !c.IsDeleted)
                    .OrderByDescending(c => c.VisitDate)
                    .ToListAsync(),
                DailyVitalLogs = await _context.DailyVitalLogs
                    .Where(d => d.PatientId == patientId && !d.IsDeleted)
                    .OrderByDescending(d => d.LoggedAt)
                    .Take(30)
                    .ToListAsync(),

                // ĐÓNG GÓI VÀO PAGED RESULT
                AiPredictions = new PagedResult<AiriskPrediction>
                {
                    Items = pagedAiItems,
                    TotalCount = totalAiCount,
                    Page = page,
                    PageSize = pageSize
                },

                WarningAlerts = await _context.WarningAlerts
                    .Where(w => w.PatientId == patientId && !w.IsDeleted)
                    .OrderByDescending(w => w.FlaggedAt)
                    .ToListAsync()
            };

            return View(model);
        }
    }
}