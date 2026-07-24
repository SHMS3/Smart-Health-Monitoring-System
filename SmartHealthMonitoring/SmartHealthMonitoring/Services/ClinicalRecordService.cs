using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services
{
    public class ClinicalRecordService : IClinicalRecordService
    {
        private readonly ClinicalRecordRepository _repository;
        private readonly IAuditLogService _auditLogService;

        public ClinicalRecordService(ClinicalRecordRepository repository, IAuditLogService auditLogService)
        {
            _repository = repository;
            _auditLogService = auditLogService;
        }

        public async Task<int?> GetPatientIdByEmailAsync(string email)
        {
            var patient = await _repository.GetPatientByEmailAsync(email);
            return patient?.Id;
        }

        public async Task<(bool success, string message, PatientRecordIndexViewModel? viewModel, int? redirectPatientId)> GetPatientRecordIndexViewModelAsync(
            int id, 
            string currentEmail, 
            bool isPatientRole, 
            bool isDoctorRole,
            int page, 
            int pageSize, 
            int diaryPage, 
            int diaryPageSize, 
            DateTime? searchDate, 
            string activeTab)
        {
            if (isPatientRole)
            {
                var currentPatient = await _repository.GetPatientByEmailAsync(currentEmail);
                if (currentPatient == null || currentPatient.Id != id)
                {
                    return (false, "Forbidden", null, null);
                }
            }

            var patient = await _repository.GetPatientByIdAsync(id);
            if (patient == null)
            {
                return (false, "Không tìm thấy bệnh nhân.", null, null);
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - patient.DateOfBirth.Year - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

            var clinicalQuery = _repository.GetClinicalRecordsQuery(id, isPatientRole);
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

            var dailyLogsQuery = _repository.GetDailyVitalLogsQuery(id, searchDate);
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

            var todayDate = DateTime.UtcNow.Date;
            int todayPaidPaymentsCount = await _repository.GetTodayPaidPaymentsCountAsync(patient.Id, todayDate);
            int todayClinicalRecordsCount = await _repository.GetTodayClinicalRecordsCountAsync(patient.Id, todayDate);

            bool hasPaidPaymentToday = todayPaidPaymentsCount > todayClinicalRecordsCount;
            bool hasClinicalRecordToday = todayClinicalRecordsCount > 0;
            bool hasConfiguredThresholds = await _repository.HasConfiguredThresholdsAsync(patient.Id);

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
                HasClinicalRecordToday = hasClinicalRecordToday,
                HasConfiguredThresholds = hasConfiguredThresholds,
                SearchDate = searchDate,
                ActiveTab = activeTab
            };

            return (true, "", viewModel, null);
        }

        public async Task<(bool success, string message, int? redirectPatientId)> DeleteClinicalRecordAsync(int recordId)
        {
            var record = await _repository.GetClinicalRecordByIdAsync(recordId);
            if (record == null)
            {
                return (false, "Không tìm thấy hồ sơ hệ thống.", null);
            }

            if (record.IsDeleted)
            {
                return (false, "Hồ sơ này đã được đánh dấu hủy từ trước.", record.PatientId);
            }

            record.IsDeleted = true;
            await _repository.UpdateClinicalRecordAsync(record);

            await _auditLogService.LogAsync(
                "Void",
                "ClinicalRecord",
                record.Id.ToString(),
                $"Hủy hồ sơ lâm sàng #{record.Id} của bệnh nhân {record.Patient.User.FullName}.",
                record.Patient.UserId,
                record.Patient.User.FullName);

            return (true, "Đã đánh dấu hủy hồ sơ thành công.", record.PatientId);
        }

        public async Task<(bool success, string message, int? redirectPatientId)> ToggleViewForPatientAsync(int recordId)
        {
            var record = await _repository.GetClinicalRecordByIdAsync(recordId);
            if (record == null || record.IsDeleted)
            {
                return (false, "Không tìm thấy hồ sơ.", null);
            }

            record.IsViewForPatient = !record.IsViewForPatient;
            await _repository.UpdateClinicalRecordAsync(record);

            await _auditLogService.LogAsync(
                record.IsViewForPatient ? "GrantAccess" : "RevokeAccess",
                "ClinicalRecord",
                record.Id.ToString(),
                record.IsViewForPatient
                    ? $"Cho phép bệnh nhân {record.Patient.User.FullName} xem hồ sơ lâm sàng #{record.Id}."
                    : $"Ẩn hồ sơ lâm sàng #{record.Id} khỏi bệnh nhân {record.Patient.User.FullName}.",
                record.Patient.UserId,
                record.Patient.User.FullName);

            string msg = record.IsViewForPatient
                ? "Đã cho phép bệnh nhân xem hồ sơ này."
                : "Đã ẩn hồ sơ này với bệnh nhân.";

            return (true, msg, record.PatientId);
        }
    }
}
