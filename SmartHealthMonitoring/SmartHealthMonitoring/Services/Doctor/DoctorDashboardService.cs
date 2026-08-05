using SmartHealthMonitoring.ViewModels.Doctor;
using SmartHealthMonitoring.ViewModels.Doctor;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Doctor;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartHealthMonitoring.Interfaces;

namespace SmartHealthMonitoring.Services.Doctor
{
    public class DoctorDashboardService : IDoctorDashboardService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailTriggerService _emailTriggerService;

        public DoctorDashboardService(SmartHealthMonitoringContext context, IEmailTriggerService emailTriggerService)
        {
            _context = context;
            _emailTriggerService = emailTriggerService;
        }

        public async Task<Models.Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
        }

        public async Task<bool> ToggleShiftAsync(int userId)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (doctor == null) return false;
            
            doctor.IsOnShift = !doctor.IsOnShift;
            await _context.SaveChangesAsync();
            return doctor.IsOnShift;
        }

        public async Task<PagedResult<PatientListViewModel>> GetPatientListAsync(int page, int pageSize)
        {
            var today = DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
            var query = _context.Patients
                .Include(p => p.User)
                .Where(p => !p.IsDeleted && !p.User.IsDeleted && p.User.Role == 0);

            int totalRecords = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.User.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PatientListViewModel
                {
                    PatientId = p.Id,
                    FullName = p.User.FullName,
                    Age = today.Year - p.DateOfBirth.Year - (today.DayOfYear < p.DateOfBirth.DayOfYear ? 1 : 0),
                    SexDisplay = p.Sex == 1 ? "Nam" : "N?",
                    Phone = p.Phone ?? "N/A"
                })
                .ToListAsync();

            return new PagedResult<PatientListViewModel>
            {
                Items = items,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PatientProfileViewModel?> GetPatientProfileAsync(int patientId, int aiPage, int aiPageSize)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);

            if (patient == null) return null;

            var aiQuery = _context.AiriskPredictions
                .Where(a => a.PatientId == patientId && !a.IsDeleted);

            int totalAiCount = await aiQuery.CountAsync();

            var pagedAiItems = await aiQuery
                .OrderByDescending(a => a.PredictedAt)
                .Skip((aiPage - 1) * aiPageSize)
                .Take(aiPageSize)
                .ToListAsync();

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
                AiPredictions = new PagedResult<AiriskPrediction>
                {
                    Items = pagedAiItems,
                    TotalCount = totalAiCount,
                    Page = aiPage,
                    PageSize = aiPageSize
                },
                WarningAlerts = await _context.WarningAlerts
                    .Where(w => w.PatientId == patientId && !w.IsDeleted)
                    .OrderByDescending(w => w.FlaggedAt)
                    .ToListAsync()
            };

            return model;
        }

        public async Task<(PagedResult<WaitingPatient> WaitingPatients, List<int> PatientsWithPayments)> GetWaitingListAsync(int doctorId, int page, int pageSize)
        {
            var today = SmartHealthMonitoring.Common.AppTime.Now.Date;

            var query = _context.WaitingPatients
                .Include(w => w.Patient).ThenInclude(p => p.User)
                .Where(w => w.CreatedAt >= today
                         && w.DoctorId == doctorId
                         && (w.Status == 0 || w.Status == 1));

            var totalCount = await query.CountAsync();

            var waitingPatients = await query
                .OrderBy(w => w.SequenceNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedResult = new PagedResult<WaitingPatient>
            {
                Items = waitingPatients,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            var patientIds = waitingPatients.Select(w => w.PatientId).ToList();
            var patientsWithPayments = await _context.Payments
                .Where(p => patientIds.Contains(p.PatientId) && p.CreatedAt.Date == today)
                .Select(p => p.PatientId)
                .Distinct()
                .ToListAsync();

            return (pagedResult, patientsWithPayments);
        }

        public async Task<bool> CancelExamAsync(int waitingPatientId, int doctorId)
        {
            var waiting = await _context.WaitingPatients.FirstOrDefaultAsync(w => w.Id == waitingPatientId && w.Status == 1 && w.DoctorId == doctorId);
            if (waiting != null)
            {
                waiting.Status = 2;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> CompleteExamAsync(int patientId, int doctorId)
        {
            var activeWaiting = await _context.WaitingPatients
                .FirstOrDefaultAsync(w => w.PatientId == patientId && (w.Status == 0 || w.Status == 1) && w.DoctorId == doctorId);
            
            if (activeWaiting != null)
            {
                activeWaiting.Status = 3;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<(bool Success, string Message, int PatientId)> AcceptPatientAsync(int waitingPatientId, int doctorId)
        {
            var waitingPatient = await _context.WaitingPatients.AsNoTracking().FirstOrDefaultAsync(w => w.Id == waitingPatientId);
            if (waitingPatient == null)
                return (false, "Kh�ng t�m th?y b?nh nh�n trong h�ng d?i.", 0);

            if (waitingPatient.Status != 0)
                return (false, "B?nh nh�n n�y d� du?c ti?p nh?n ho?c d� h?y.", 0);

            int rowsAffected = await _context.WaitingPatients
                .Where(w => w.Id == waitingPatientId && w.Status == 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.Status, 1)
                    .SetProperty(w => w.DoctorId, doctorId)
                    .SetProperty(w => w.AcceptedAt, SmartHealthMonitoring.Common.AppTime.Now));

            if (rowsAffected == 0)
            {
                return (false, "C?nh b�o: B?nh nh�n n�y v?a du?c m?t b�c si kh�c ti?p nh?n!", 0);
            }

            try
            {
                await _emailTriggerService.SendDoctorAcceptedCheckInAsync(waitingPatientId, doctorId);
            }
            catch (Exception emailEx)
            {
                Console.WriteLine($"[AcceptPatient Email] {emailEx.Message}");
            }

            return (true, "Th�nh c�ng", waitingPatient.PatientId);
        }

        public async Task<List<Service>> GetActiveServicesAsync()
        {
            return await _context.Services
                .Where(s => s.IsActive)
                .ToListAsync();
        }

        public async Task<(bool Success, string Message)> CreatePaymentAsync(CreatePaymentRequest request, int doctorId)
        {
            if (request.ServiceIds == null || !request.ServiceIds.Any())
                return (false, "Vui l�ng ch?n �t nh?t m?t d?ch v?.");

            var services = await _context.Services
                .Where(s => request.ServiceIds.Contains(s.Id) && s.IsActive)
                .ToListAsync();

            if (!services.Any())
                return (false, "C�c d?ch v? d� ch?n kh�ng h?p l?.");

            var payment = new Payment
            {
                PatientId = request.PatientId,
                DoctorId = doctorId,
                TotalAmount = services.Sum(s => s.Price),
                Status = "Pending",
                CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var paymentDetails = services.Select(s => new PaymentDetail
            {
                PaymentId = payment.Id,
                ServiceId = s.Id,
                PriceAtTime = s.Price
            }).ToList();

            _context.PaymentDetails.AddRange(paymentDetails);
            await _context.SaveChangesAsync();

            return (true, "�� g?i y�u c?u thanh to�n th�nh c�ng.");
        }

        public async Task<int> GetUnresolvedAlertCountAsync()
        {
            return await _context.WarningAlerts
                .CountAsync(w => w.Status == 0 && !w.IsDeleted);
        }
    }
}




