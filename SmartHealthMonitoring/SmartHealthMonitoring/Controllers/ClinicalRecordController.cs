using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "0,1")] // Cho phép cả Bệnh nhân và Bác sĩ đi qua cổng Controller
    public class ClinicalRecordController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public ClinicalRecordController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "0")]
        public async Task<IActionResult> MyRecords()
        {
            var email = User.Identity?.Name;

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p =>
                    p.User.Email == email &&
                    !p.IsDeleted);

            if (patient == null)
            {
                return Forbid();
            }

            return RedirectToAction(nameof(Index), new { id = patient.Id });
        }

        [HttpGet]
        [Authorize(Roles = "0,1")]
        public async Task<IActionResult> Index(int id, int page = 1, int pageSize = 10)
        {
            try
            {
                // Nếu là Patient -> chỉ được xem hồ sơ của chính mình
                if (User.IsInRole("0"))
                {
                    var email = User.Identity?.Name;

                    var currentPatient = await _context.Patients
                        .Include(p => p.User)
                        .FirstOrDefaultAsync(p =>
                            p.User.Email == email &&
                            !p.IsDeleted);

                    // Patient cố xem hồ sơ người khác
                    if (currentPatient == null || currentPatient.Id != id)
                    {
                        return Forbid();
                    }
                }

                // Doctor hoặc patient hợp lệ mới tới đây
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

                if (patient == null)
                {
                    TempData["Error"] = "Không tìm thấy bệnh nhân.";

                    if (User.IsInRole("1"))
                    {
                        return RedirectToAction("Index", "DoctorDashboard");
                    }

                    return RedirectToAction("Index", "Home");
                }

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var age = today.Year - patient.DateOfBirth.Year - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

                // 1. Dựng Query danh sách hồ sơ
                var query = _context.ClinicalRecords
                    .Where(r => r.PatientId == id && !r.IsDeleted)
                    .OrderByDescending(r => r.VisitDate);

                // 2. Đếm tổng số bản ghi
                int totalRecords = await query.CountAsync();

                // 3. Phân trang bằng Skip & Take
                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new ClinicalRecordSummaryViewModel
                    {
                        Id = r.Id,
                        VisitDate = r.VisitDate,
                        RestingBP = r.RestingBp,
                        Cholesterol = r.Cholesterol,
                        MaxHeartRate = r.MaxHeartRate,
                        ChestPainTypeDisplay = GetChestPainDisplay(r.ChestPainType),

                        FastingBS = r.FastingBs,
                        RestECG = r.RestEcg,
                        ExerciseAngina = r.ExerciseAngina,
                        OldPeak = r.OldPeak,
                        STSlope = r.Stslope,
                        MajorVessels = r.MajorVessels,
                        ThalResult = r.ThalResult
                    })
                    .ToListAsync();

                // 4. Gói dữ liệu vào ViewModel
                var viewModel = new PatientRecordIndexViewModel
                {
                    PatientId = patient.Id,
                    PatientName = patient.User.FullName,
                    Age = age,
                    SexDisplay = patient.Sex == 1 ? "Nam" : "Nữ",

                    Records = new PagedResult<ClinicalRecordSummaryViewModel>
                    {
                        Items = items,
                        TotalCount = totalRecords,
                        Page = page,
                        PageSize = pageSize
                    }
                };

                return View(viewModel);
            }
            catch (Exception)
            {
                TempData["Error"] = "Lỗi khi tải hồ sơ y tế.";

                if (User.IsInRole("1"))
                {
                    return RedirectToAction("Index", "DoctorDashboard");
                }

                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [Authorize(Roles = "1")] // Chỉ Bác sĩ mới được quyền Hủy hồ sơ
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var record = await _context.ClinicalRecords.FirstOrDefaultAsync(r => r.Id == id);

                if (record == null)
                {
                    TempData["Error"] = "Không tìm thấy hồ sơ hệ thống.";
                    return RedirectToAction("Index", "DoctorDashboard");
                }

                if (record.IsDeleted)
                {
                    TempData["Error"] = "Hồ sơ này đã được đánh dấu hủy từ trước.";
                    return RedirectToAction(nameof(Index), new { id = record.PatientId });
                }

                // Chuyển trạng thái sang Soft Delete (Void)
                record.IsDeleted = true;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã đánh dấu hủy hồ sơ thành công.";
                return RedirectToAction(nameof(Index), new { id = record.PatientId });
            }
            catch (Exception)
            {
                TempData["Error"] = "Lỗi hệ thống khi hủy hồ sơ.";
                return RedirectToAction("Index", "DoctorDashboard");
            }
        }

        private static string GetChestPainDisplay(byte type)
        {
            return type switch
            {
                0 => "Typical Angina (TA)",
                1 => "Atypical Angina (ATA)",
                2 => "Non-Anginal Pain (NAP)",
                3 => "Asymptomatic (ASY)",
                _ => "Unknown"
            };
        }
    }
}