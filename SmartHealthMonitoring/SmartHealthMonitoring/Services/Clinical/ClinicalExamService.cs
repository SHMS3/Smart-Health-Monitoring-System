using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Audit;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Interfaces.Minio;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.Interfaces.Clinical;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services.Clinical
{
    public class ClinicalExamService : IClinicalExamService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IMinioService _minioService;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;

        public ClinicalExamService(
            SmartHealthMonitoringContext context,
            IMinioService minioService,
            IAuditLogService auditLogService,
            IEmailService emailService)
        {
            _context = context;
            _minioService = minioService;
            _auditLogService = auditLogService;
            _emailService = emailService;
        }

        public async Task<SmartHealthMonitoring.Models.Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
        }

        public async Task<(Payment? payment, List<string> purchasedServiceNames)> GetAvailablePaymentAsync(int patientId, int doctorId)
        {
            var today = SmartHealthMonitoring.Common.AppTime.Now.Date;

            var paidPayments = await _context.Payments
                .Include(p => p.PaymentDetails)
                    .ThenInclude(pd => pd.Service)
                .Where(p => p.PatientId == patientId && p.DoctorId == doctorId && p.Status == "Paid" && p.CreatedAt >= today)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            int recordsCount = await _context.ClinicalRecords
                .CountAsync(r => r.PatientId == patientId && r.DoctorId == doctorId && r.VisitDate >= today && !r.IsDeleted);

            Payment? availablePayment = null;
            if (recordsCount < paidPayments.Count)
            {
                availablePayment = paidPayments[recordsCount];
            }

            var purchasedServiceNames = availablePayment?.PaymentDetails
                .Select(pd => pd.Service.Name.ToLower())
                .ToList() ?? new List<string>();

            return (availablePayment, purchasedServiceNames);
        }

        public async Task<ClinicalRecord?> CreateClinicalExamAsync(ClinicalExamFormViewModel model, int doctorId)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted);
            if (doctor == null) return null;

            if (model.AttachmentFile != null && model.AttachmentFile.Length > 0)
            {
                using (var stream = model.AttachmentFile.OpenReadStream())
                {
                    string extension = Path.GetExtension(model.AttachmentFile.FileName);
                    string objectName = $"attach_{model.PatientId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
                    string bucketName = "clinical-attachments";

                    await _minioService.UploadFileAsync(bucketName, objectName, stream, model.AttachmentFile.ContentType);
                    model.AttachmentUrl = await _minioService.GetPresignedUrlAsync(bucketName, objectName, 10080);
                }
            }

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == model.PatientId && !p.IsDeleted);

            bool isAutoThresholdCreated = false;
            PatientThreshold autoThreshold = null;
            if (patient != null)
            {
                var existingThreshold = await _context.PatientThresholds.FirstOrDefaultAsync(t => t.PatientId == model.PatientId);
                if (existingThreshold == null)
                {
                    var todayDate = DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
                    int age = todayDate.Year - patient.DateOfBirth.Year - (todayDate.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

                    var templates = await _context.StandardThresholds
                        .Where(t => t.IsActive && age >= t.AgeMin && age <= t.AgeMax)
                        .ToListAsync();

                    var matched = templates.FirstOrDefault(t => t.Sex == patient.Sex) ?? templates.FirstOrDefault(t => t.Sex == 2);

                    if (matched != null)
                    {
                        autoThreshold = new PatientThreshold
                        {
                            PatientId = model.PatientId,
                            SystolicBpWarning = matched.SystolicBpWarning,
                            SystolicBpDanger = matched.SystolicBpDanger,
                            DiastolicBpWarning = matched.DiastolicBpWarning,
                            DiastolicBpDanger = matched.DiastolicBpDanger,
                            HeartRateWarningMin = matched.HeartRateWarningMin,
                            HeartRateDangerMin = matched.HeartRateDangerMin,
                            HeartRateWarningMax = matched.HeartRateWarningMax,
                            HeartRateDangerMax = matched.HeartRateDangerMax,
                            UpdatedAt = DateTime.Now,
                            UpdatedByDoctorId = doctor.Id
                        };
                        _context.PatientThresholds.Add(autoThreshold);
                        isAutoThresholdCreated = true;
                    }
                }
            }

            var record = new ClinicalRecord
            {
                PatientId = model.PatientId,
                DoctorId = doctor.Id,
                VisitDate = DateTime.Now,
                ChestPainType = model.ChestPainType,
                RestingBp = model.RestingBP,
                Cholesterol = model.Cholesterol,
                FastingBs = model.FastingBS,
                RestEcg = model.RestECG,
                MaxHeartRate = model.MaxHeartRate,
                ExerciseAngina = model.ExerciseAngina,
                OldPeak = model.OldPeak,
                Stslope = model.STSlope,
                MajorVessels = model.MajorVessels,
                ThalResult = model.ThalResult,
                EcgImageUrl = model.EcgImageUrl,
                AttachmentUrl = model.AttachmentUrl,
                IsDeleted = false,
                IsViewForPatient = model.IsViewForPatient
            };

            _context.ClinicalRecords.Add(record);
            await _context.SaveChangesAsync();

            var activeWaiting = await _context.WaitingPatients
                .FirstOrDefaultAsync(w => w.PatientId == model.PatientId && (w.Status == 0 || w.Status == 1));
            if (activeWaiting != null)
            {
                activeWaiting.Status = 3;
                await _context.SaveChangesAsync();
            }

            var examToday = SmartHealthMonitoring.Common.AppTime.Now.Date;
            var activeAppointment = await _context.Appointments
                .Include(a => a.Slot)
                .FirstOrDefaultAsync(a => a.PatientId == model.PatientId
                                       && a.Slot.DoctorId == doctor.Id
                                       && a.Status == AppointmentStatus.Confirmed
                                       && a.Slot.SlotStart.Date == examToday);

            if (activeAppointment != null)
            {
                activeAppointment.Status = AppointmentStatus.Completed;
                activeAppointment.ClinicalRecordId = record.Id;
                activeAppointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;
                activeAppointment.Slot.Status = AppointmentSlotStatus.Completed;
                await _context.SaveChangesAsync();
            }

            await _auditLogService.LogAsync(
                "Create",
                "ClinicalRecord",
                record.Id.ToString(),
                $"T?o h? so l�m s�ng #{record.Id} cho b?nh nh�n {patient?.User?.FullName ?? $"#{model.PatientId}"}; huy?t �p {record.RestingBp}, cholesterol {record.Cholesterol}, nh?p tim t?i da {record.MaxHeartRate}.",
                patient?.UserId,
                patient?.User?.FullName);

            if (isAutoThresholdCreated && autoThreshold != null)
            {
                await _auditLogService.LogAsync(
                    "Create",
                    "PatientThreshold",
                    autoThreshold.Id.ToString(),
                    $"T? d?ng c?u h�nh ngu?ng cho b?nh nh�n {patient?.User?.FullName ?? $"#{model.PatientId}"}; huy?t �p t�m thu {autoThreshold.SystolicBpWarning}/{autoThreshold.SystolicBpDanger}, nh?p tim {autoThreshold.HeartRateWarningMin}-{autoThreshold.HeartRateWarningMax}.",
                    patient?.UserId,
                    patient?.User?.FullName);
            }

            return record;
        }

        public async Task<StandardThreshold?> GetSuggestedThresholdAsync(int patientId, int doctorId)
        {
            var patient = await _context.Patients.FindAsync(patientId);
            if (patient == null) return null;
            
            var today = DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
            int age = today.Year - patient.DateOfBirth.Year - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

            var templates = await _context.StandardThresholds
                .Where(t => t.IsActive && age >= t.AgeMin && age <= t.AgeMax)
                .ToListAsync();

            var matched = templates.FirstOrDefault(t => t.Sex == patient.Sex) ?? templates.FirstOrDefault(t => t.Sex == 2);
            return matched;
        }

        public async Task<List<StandardThreshold>> GetAllStandardThresholdsAsync()
        {
            return await _context.StandardThresholds
                .Where(t => t.IsActive)
                .OrderBy(t => t.Sex)
                .ThenBy(t => t.AgeMin)
                .ToListAsync();
        }

        public async Task<PatientThreshold?> GetPatientThresholdAsync(int patientId)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .Include(p => p.PatientThreshold)
                    .ThenInclude(t => t!.UpdatedByDoctor)
                        .ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);

            return patient?.PatientThreshold;
        }

        public async Task<bool> SavePatientThresholdAsync(int patientId, int doctorId, PatientThresholdViewModel model)
        {
            var existing = await _context.PatientThresholds.FirstOrDefaultAsync(t => t.PatientId == patientId);
            var isNewThreshold = existing == null;
            PatientThreshold threshold;

            if (existing == null)
            {
                threshold = new PatientThreshold
                {
                    PatientId = patientId,
                    SystolicBpWarning = model.SystolicBpWarning,
                    SystolicBpDanger = model.SystolicBpDanger,
                    DiastolicBpWarning = model.DiastolicBpWarning,
                    DiastolicBpDanger = model.DiastolicBpDanger,
                    HeartRateWarningMin = model.HeartRateWarningMin,
                    HeartRateDangerMin = model.HeartRateDangerMin,
                    HeartRateWarningMax = model.HeartRateWarningMax,
                    HeartRateDangerMax = model.HeartRateDangerMax,
                    UpdatedAt = DateTime.Now,
                    UpdatedByDoctorId = doctorId
                };
                _context.PatientThresholds.Add(threshold);
            }
            else
            {
                threshold = existing;
                threshold.SystolicBpWarning = model.SystolicBpWarning;
                threshold.SystolicBpDanger = model.SystolicBpDanger;
                threshold.DiastolicBpWarning = model.DiastolicBpWarning;
                threshold.DiastolicBpDanger = model.DiastolicBpDanger;
                threshold.HeartRateWarningMin = model.HeartRateWarningMin;
                threshold.HeartRateDangerMin = model.HeartRateDangerMin;
                threshold.HeartRateWarningMax = model.HeartRateWarningMax;
                threshold.HeartRateDangerMax = model.HeartRateDangerMax;
                threshold.UpdatedAt = DateTime.Now;
                threshold.UpdatedByDoctorId = doctorId;
            }

            await _context.SaveChangesAsync();

            var patientForAudit = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            await _auditLogService.LogAsync(
                isNewThreshold ? "Create" : "Update",
                "PatientThreshold",
                threshold.Id.ToString(),
                $"{(isNewThreshold ? "T?o" : "C?p nh?t")} ngu?ng ri�ng cho b?nh nh�n {patientForAudit?.User?.FullName ?? $"#{patientId}"}; huy?t �p t�m thu {threshold.SystolicBpWarning}/{threshold.SystolicBpDanger}, nh?p tim {threshold.HeartRateWarningMin}-{threshold.HeartRateWarningMax}.",
                patientForAudit?.UserId,
                patientForAudit?.User?.FullName);
            return true;
        }
    }
}

