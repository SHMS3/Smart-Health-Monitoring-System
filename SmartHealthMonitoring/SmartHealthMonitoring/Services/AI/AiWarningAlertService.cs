using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services.AI
{
    /// <summary>
    /// Scoped Service triển khai logic quản lý WarningAlert:
    /// phân trang + tìm kiếm theo keyword, claim, resolve.
    /// </summary>
    public class AiWarningAlertService : IAiWarningAlertService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AiWarningAlertService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }
        public async Task<WarningAlert?> GetByIdAsync(int id)
        {
            return await _context.WarningAlerts
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<ServiceResult> ClaimAlertAsync(
      int alertId,
      int doctorId)
        {
            var alert = await _context.WarningAlerts
                .FirstOrDefaultAsync(x =>
                    x.Id == alertId &&
                    !x.IsDeleted);

            if (alert == null)
            {
                return ServiceResult.Fail(
                    "Cảnh báo không tồn tại.");
            }

            if (alert.Status != 0)
            {
                return ServiceResult.Fail(
                    "Cảnh báo đã được bác sĩ khác tiếp nhận.");
            }

            alert.ClaimedByDoctorId = doctorId;
            alert.ClaimedAt = DateTime.Now;
            alert.Status = 1;

            try
            {
                await _context.SaveChangesAsync();

                return ServiceResult.Ok(
                    "Tiếp nhận cảnh báo thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult.Fail(
                    "Cảnh báo đã được bác sĩ khác tiếp nhận.");
            }
            catch
            {
                return ServiceResult.Fail(
                    "Có lỗi xảy ra khi tiếp nhận cảnh báo.");
            }
        }



        public async Task<List<WarningAlert>> GetAlertsAsync(byte? status, string? keyword, int page, int pageSize, int? claimedByDoctorId = null)
        {
            var query = _context.WarningAlerts
                .Where(x => !x.IsDeleted)
                .Include(x => x.Patient)
                    .ThenInclude(p => p.User)
                .Include(x => x.Patient)
                    .ThenInclude(p => p.PatientThreshold)
                .Include(x => x.ClaimedByDoctor)
                    .ThenInclude(d => d.User)
                .Include(x => x.Prediction)
                .AsQueryable();

            // Filter Status
            if (status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == status.Value);
            }

            // Search Patient Name
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.Patient.User.FullName
                        .Contains(keyword));
            }

            // Filter Doctor Claimed
            if (claimedByDoctorId.HasValue)
            {
                query = query.Where(x =>
                    x.ClaimedByDoctorId == claimedByDoctorId.Value);
            }

            return await query
                .OrderByDescending(x => x.FlaggedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalAlertsAsync(byte? status, string? keyword, int? claimedByDoctorId = null)
        {
            var query = _context.WarningAlerts
                .Where(x => !x.IsDeleted)
                .Include(x => x.Patient)
                    .ThenInclude(p => p.User)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.Patient.User.FullName
                        .Contains(keyword));
            }

            if (claimedByDoctorId.HasValue)
            {
                query = query.Where(x =>
                    x.ClaimedByDoctorId == claimedByDoctorId.Value);
            }

            return await query.CountAsync();
        }

        public async Task<ServiceResult> ResolveAlertAsync(
    int alertId,
    int doctorId,
    string resolutionNote)
        {
            var alert = await _context.WarningAlerts
                .FirstOrDefaultAsync(x =>
                    x.Id == alertId &&
                    !x.IsDeleted);

            if (alert == null)
            {
                return ServiceResult.Fail(
                    "Cảnh báo không tồn tại.");
            }

            if (alert.Status != 1)
            {
                return ServiceResult.Fail(
                    "Cảnh báo chưa được tiếp nhận hoặc đã xử lý.");
            }

            if (alert.ClaimedByDoctorId != doctorId)
            {
                return ServiceResult.Fail(
                    "Bạn không phải bác sĩ đang xử lý cảnh báo này.");
            }

            alert.Status = 2;
            alert.ResolutionNote = resolutionNote;

            try
            {
                await _context.SaveChangesAsync();

                return ServiceResult.Ok(
                    "Xử lý cảnh báo thành công.");
            }
            catch
            {
                return ServiceResult.Fail(
                    "Có lỗi xảy ra khi cập nhật cảnh báo.");
            }
        }
        public async Task<WarningAlertDetailViewModel?> GetDetailAsync(int id)
        {
            var alert = await _context.WarningAlerts
                .Include(x => x.Patient)
                    .ThenInclude(x => x.User)
                .Include(x => x.Patient)
                     .ThenInclude(x => x.PatientThreshold)

                .Include(x => x.Patient)
                    .ThenInclude(x => x.ClinicalRecords)
                    .Include(x => x.Patient)
            .ThenInclude(x => x.ClinicalRecords)
                .ThenInclude(x => x.Doctor)
                    .ThenInclude(x => x.User)

                .Include(x => x.Patient)
                    .ThenInclude(x => x.DailyVitalLogs)

                .Include(x => x.Prediction)

                .Include(x => x.ClaimedByDoctor)
                    .ThenInclude(x => x.User)

                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    !x.IsDeleted);

            if (alert == null)
                return null;

            return new WarningAlertDetailViewModel
            {
                Alert = alert,

                RecentVitalLogs = alert.Patient
            .DailyVitalLogs
            .OrderByDescending(x => x.LoggedAt)
            .Take(10)
            .ToList(),

                ClinicalRecords = alert.Patient
            .ClinicalRecords
            .OrderByDescending(x => x.VisitDate)
            .Take(10)
            .ToList()
            };
        }
        public async Task<WarningAlert?> GetAlertForResolveAsync(int id)
        {
            return await _context.WarningAlerts
                .Include(x => x.Patient)
                    .ThenInclude(p => p.User)
                .Include(x => x.Patient)
                    .ThenInclude(p => p.PatientThreshold)
                .Include(x => x.ClaimedByDoctor)
                    .ThenInclude(d => d.User)
                .Include(x => x.Prediction)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }
    }
}
