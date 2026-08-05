using SmartHealthMonitoring.Interfaces.Audit;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Interfaces.Clinical;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services.Clinical
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
                return (false, "Kh�ng t�m th?y b?nh nh�n.", null, null);
            }

            var today = DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
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

            var todayDate = SmartHealthMonitoring.Common.AppTime.Now.Date;
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
                SexDisplay = patient.Sex == 1 ? "Nam" : "N?",

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
                return (false, "Kh�ng t�m th?y h? so h? th?ng.", null);
            }

            if (record.IsDeleted)
            {
                return (false, "H? so n�y d� du?c d�nh d?u h?y t? tru?c.", record.PatientId);
            }

            record.IsDeleted = true;
            await _repository.UpdateClinicalRecordAsync(record);

            await _auditLogService.LogAsync(
                "Void",
                "ClinicalRecord",
                record.Id.ToString(),
                $"H?y h? so l�m s�ng #{record.Id} c?a b?nh nh�n {record.Patient.User.FullName}.",
                record.Patient.UserId,
                record.Patient.User.FullName);

            return (true, "�� d�nh d?u h?y h? so th�nh c�ng.", record.PatientId);
        }

        public async Task<(bool success, string message, int? redirectPatientId)> ToggleViewForPatientAsync(int recordId)
        {
            var record = await _repository.GetClinicalRecordByIdAsync(recordId);
            if (record == null || record.IsDeleted)
            {
                return (false, "Kh�ng t�m th?y h? so.", null);
            }

            record.IsViewForPatient = !record.IsViewForPatient;
            await _repository.UpdateClinicalRecordAsync(record);

            await _auditLogService.LogAsync(
                record.IsViewForPatient ? "GrantAccess" : "RevokeAccess",
                "ClinicalRecord",
                record.Id.ToString(),
                record.IsViewForPatient
                    ? $"Cho ph�p b?nh nh�n {record.Patient.User.FullName} xem h? so l�m s�ng #{record.Id}."
                    : $"?n h? so l�m s�ng #{record.Id} kh?i b?nh nh�n {record.Patient.User.FullName}.",
                record.Patient.UserId,
                record.Patient.User.FullName);

            string msg = record.IsViewForPatient
                ? "�� cho ph�p b?nh nh�n xem h? so n�y."
                : "�� ?n h? so n�y v?i b?nh nh�n.";

            return (true, msg, record.PatientId);
        }
    }
}



