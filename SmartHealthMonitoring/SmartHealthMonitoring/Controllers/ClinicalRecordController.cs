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
                        .FirstOrDefaultAsync(p => p.User.Email == email && !p.IsDeleted);

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
                    return User.IsInRole("1") ? RedirectToAction("Index", "DoctorDashboard") : RedirectToAction("Index", "Home");
                }

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var age = today.Year - patient.DateOfBirth.Year - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

                // ========================================================
                // TAB 1: Dựng Query và Phân trang danh sách Cận lâm sàng
                // ========================================================
                var clinicalQuery = _context.ClinicalRecords
                    .Where(r => r.PatientId == id && !r.IsDeleted)
                    .OrderByDescending(r => r.VisitDate);

                int totalRecords = await clinicalQuery.CountAsync();

                var clinicalItems = await clinicalQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new ClinicalRecordSummaryViewModel
                    {
                        Id = r.Id,
                        VisitDate = r.VisitDate,
                        RestingBP = r.RestingBp,
                        Cholesterol = r.Cholesterol,
                        MaxHeartRate = r.MaxHeartRate,

                        // ĐÃ FIX: Dùng toán tử 3 ngôi để EF Core có thể dịch sang SQL (CASE WHEN)
                        ChestPainTypeDisplay = r.ChestPainType == 0 ? "Typical Angina (TA)" :
                                               r.ChestPainType == 1 ? "Atypical Angina (ATA)" :
                                               r.ChestPainType == 2 ? "Non-Anginal Pain (NAP)" : "Asymptomatic (ASY)",

                        FastingBS = r.FastingBs,
                        RestECG = r.RestEcg,
                        ExerciseAngina = r.ExerciseAngina,
                        OldPeak = r.OldPeak,
                        STSlope = r.Stslope,
                        MajorVessels = r.MajorVessels,
                        ThalResult = r.ThalResult,
                        EcgImageUrl = r.EcgImageUrl,
                        AttachmentUrl = r.AttachmentUrl
                    })
                    .ToListAsync();

                // ========================================================
                // TAB 2: Lấy danh sách Sổ tay tại nhà của bệnh nhân
                // ========================================================
                var dailyLogs = await _context.DailyVitalLogs
                    .Where(d => d.PatientId == id && !d.IsDeleted)
                    .OrderByDescending(d => d.LoggedAt)
                    .Take(30) // Lấy 30 bản ghi gần nhất để giao diện load mượt
                    .Select(d => new DailyVitalLogViewModel
                    {
                        Id = d.Id,
                        LoggedAt = d.LoggedAt,
                        SystolicBp = d.SystolicBp,
                        DiastolicBp = d.DiastolicBp,
                        HeartRate = d.HeartRate,
                        ChestPainLevel = d.ChestPainLevel,
                        HasExerciseAngina = d.HasExerciseAngina,
                        UpdateCount = d.UpdateCount
                    })
                    .ToListAsync();

                // ========================================================
                // Gói toàn bộ dữ liệu vào ViewModel chung
                // ========================================================
                var viewModel = new PatientRecordIndexViewModel
                {
                    PatientId = patient.Id,
                    PatientName = patient.User.FullName,
                    Age = age,
                    SexDisplay = patient.Sex == 1 ? "Nam" : "Nữ",

                    Records = new PagedResult<ClinicalRecordSummaryViewModel>
                    {
                        Items = clinicalItems,
                        TotalCount = totalRecords,
                        Page = page,
                        PageSize = pageSize
                    },

                    DailyLogs = dailyLogs
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Thêm ex.Message để sau này nếu có lỗi thì nó hiện rõ nguyên nhân, dễ debug hơn
                TempData["Error"] = "Lỗi khi tải hồ sơ y tế: " + ex.Message;

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

        [HttpPost]
        [Authorize(Roles = "1")] // Chỉ Bác sĩ mới được cập nhật quyền xem
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleViewForPatient(int id)
        {
            try
            {
                var record = await _context.ClinicalRecords.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (record == null)
                {
                    TempData["Error"] = "Không tìm thấy hồ sơ.";
                    return RedirectToAction("Index", "DoctorDashboard");
                }

                // Đảo trạng thái IsViewForPatient
                record.IsViewForPatient = !record.IsViewForPatient;

                await _context.SaveChangesAsync();

                TempData["Success"] = record.IsViewForPatient
                    ? "Đã cho phép bệnh nhân xem hồ sơ này."
                    : "Đã ẩn hồ sơ này với bệnh nhân.";

                return RedirectToAction(nameof(Index), new { id = record.PatientId });
            }
            catch (Exception)
            {
                TempData["Error"] = "Lỗi hệ thống khi cập nhật quyền xem.";
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