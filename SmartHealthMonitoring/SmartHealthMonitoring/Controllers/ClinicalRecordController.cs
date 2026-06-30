using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
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
        private readonly IAuditLogService _auditLogService;

        public ClinicalRecordController(
            SmartHealthMonitoringContext context,
            IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
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
        public async Task<IActionResult> Index(int id, int page = 1, int pageSize = 10, int diaryPage = 1, int diaryPageSize = 10, DateTime? searchDate = null, string activeTab = "clinical-content")
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
                var baseQuery = _context.ClinicalRecords
                    .Where(r => r.PatientId == id && !r.IsDeleted);

                // Nếu là bệnh nhân (role 0) thì chỉ lấy hồ sơ được cho phép xem
                if (User.IsInRole("0"))
                {
                    baseQuery = baseQuery.Where(r => r.IsViewForPatient);
                }

                var clinicalQuery = baseQuery.OrderByDescending(r => r.VisitDate);

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
                        ChestPainType = r.ChestPainType,
                        // Chỉ tạo display khi ChestPainType có giá trị
                        ChestPainTypeDisplay = r.ChestPainType == null ? null :
                                               r.ChestPainType == 0 ? "Typical Angina (TA)" :
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
                        AttachmentUrl = r.AttachmentUrl,
                        IsViewForPatient = r.IsViewForPatient
                    })
                    .ToListAsync();

                // ========================================================
                // TAB 2: Lấy danh sách Sổ tay tại nhà của bệnh nhân
                // ========================================================
                var dailyLogsQuery = _context.DailyVitalLogs
                    .Where(d => d.PatientId == id && !d.IsDeleted);

                if (searchDate.HasValue)
                {
                    var dateStart = searchDate.Value.Date;
                    var dateEnd = searchDate.Value.Date.AddDays(1).AddTicks(-1);
                    dailyLogsQuery = dailyLogsQuery.Where(d => d.LoggedAt >= dateStart && d.LoggedAt <= dateEnd);
                }

                dailyLogsQuery = dailyLogsQuery.OrderByDescending(d => d.LoggedAt);

                int totalDiaryRecords = await dailyLogsQuery.CountAsync();

                var dailyLogsItems = await dailyLogsQuery
                    .Skip((diaryPage - 1) * diaryPageSize)
                    .Take(diaryPageSize)
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
                // Kiểm tra trạng thái thanh toán hôm nay (1 lần thanh toán = 1 hồ sơ)
                // ========================================================
                var todayDate = DateTime.UtcNow.Date;
                int todayPaidPaymentsCount = await _context.Payments
                    .CountAsync(p => p.PatientId == patient.Id && p.Status == "Paid" && p.CreatedAt.Date == todayDate);

                int todayClinicalRecordsCount = await _context.ClinicalRecords
                    .CountAsync(r => r.PatientId == patient.Id && r.VisitDate.Date == todayDate && !r.IsDeleted);

                bool hasPaidPaymentToday = todayPaidPaymentsCount > todayClinicalRecordsCount;

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

                    DailyLogs = new PagedResult<DailyVitalLogViewModel>
                    {
                        Items = dailyLogsItems,
                        TotalCount = totalDiaryRecords,
                        Page = diaryPage,
                        PageSize = diaryPageSize
                    },

                    HasPaidPaymentToday = hasPaidPaymentToday,
                    SearchDate = searchDate,
                    ActiveTab = activeTab
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
                var record = await _context.ClinicalRecords
                    .Include(r => r.Patient)
                        .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(r => r.Id == id);

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
                await _auditLogService.LogAsync(
                    "Void",
                    "ClinicalRecord",
                    record.Id.ToString(),
                    $"Hủy hồ sơ lâm sàng #{record.Id} của bệnh nhân {record.Patient.User.FullName}.",
                    record.Patient.UserId,
                    record.Patient.User.FullName);

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
                var record = await _context.ClinicalRecords
                    .Include(r => r.Patient)
                        .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (record == null)
                {
                    TempData["Error"] = "Không tìm thấy hồ sơ.";
                    return RedirectToAction("Index", "DoctorDashboard");
                }

                // Đảo trạng thái IsViewForPatient
                record.IsViewForPatient = !record.IsViewForPatient;

                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync(
                    record.IsViewForPatient ? "GrantAccess" : "RevokeAccess",
                    "ClinicalRecord",
                    record.Id.ToString(),
                    record.IsViewForPatient
                        ? $"Cho phép bệnh nhân {record.Patient.User.FullName} xem hồ sơ lâm sàng #{record.Id}."
                        : $"Ẩn hồ sơ lâm sàng #{record.Id} khỏi bệnh nhân {record.Patient.User.FullName}.",
                    record.Patient.UserId,
                    record.Patient.User.FullName);

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
