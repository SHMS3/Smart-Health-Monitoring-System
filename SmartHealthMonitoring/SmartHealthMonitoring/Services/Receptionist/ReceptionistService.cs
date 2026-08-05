using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Interfaces.Receptionist;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services.Receptionist
{
    public class ReceptionistService : IReceptionistService
    {
        private readonly ReceptionistRepository _repository;
        private readonly IEmailService _emailService;

        public ReceptionistService(ReceptionistRepository repository, IEmailService emailService)
        {
            _repository = repository;
            _emailService = emailService;
        }

        public async Task<PagedResult<Payment>> GetPendingPaymentsAsync(int page, int pageSize)
        {
            int totalRecords = await _repository.GetPendingPaymentsCountAsync();
            var payments = await _repository.GetPendingPaymentsAsync(page, pageSize);

            return new PagedResult<Payment>
            {
                Items = payments,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<Payment>> GetPaidPaymentsAsync(DateTime fromDate, DateTime toDate, int page, int pageSize)
        {
            var start = fromDate.Date;
            var end = toDate.Date.AddDays(1).AddTicks(-1);

            int totalRecords = await _repository.GetPaidPaymentsCountAsync(start, end);
            var payments = await _repository.GetPaidPaymentsAsync(start, end, page, pageSize);

            return new PagedResult<Payment>
            {
                Items = payments,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<Payment?> GetPaymentDetailsAsync(int id)
        {
            return await _repository.GetPaymentDetailsAsync(id);
        }

        public async Task<Payment?> GetPaymentByIdAsync(int id)
        {
            return await _repository.GetPaymentByIdAsync(id);
        }

        public async Task<(bool success, string message)> ConfirmCashAsync(int id)
        {
            var payment = await _repository.GetPaymentByIdAsync(id);
            if (payment == null)
                return (false, "Kh�ng t�m th?y phi?u thanh to�n");

            if (payment.Status != "Pending")
                return (false, "Phi?u n�y d� du?c x? l�");

            payment.Status = "Paid";
            payment.PaidAt = SmartHealthMonitoring.Common.AppTime.Now;
            payment.PaymentMethod = "Cash";

            await _repository.UpdatePaymentAsync(payment);
            return (true, "X�c nh?n thanh to�n ti?n m?t th�nh c�ng!");
        }

        public async Task<(bool paid, string message)> CheckQrPaymentStatusAsync(int id)
        {
            var payment = await _repository.GetPaymentByIdAsync(id);
            if (payment == null)
                return (false, "Kh�ng t�m th?y phi?u");

            return (payment.Status == "Paid", payment.Status);
        }

        public async Task<(bool success, string message, int? paymentId)> ProcessSepayWebhookAsync(string content, decimal amount)
        {
            var upperContent = content.ToUpper();
            var payments = await _repository.GetPendingPaymentsListAsync();

            foreach (var pmt in payments)
            {
                var expectedContent = $"THANHTOAN HD{pmt.Id:D5}";
                if (upperContent.Contains(expectedContent.ToUpper()))
                {
                    pmt.Status = "Paid";
                    pmt.PaidAt = SmartHealthMonitoring.Common.AppTime.Now;
                    pmt.PaymentMethod = "QR";
                    await _repository.UpdatePaymentAsync(pmt);
                    return (true, "Success", pmt.Id);
                }
            }

            return (false, "Kh�ng t�m th?y phi?u ph� h?p", null);
        }

        public async Task<SmartHealthMonitoring.Common.PagedResult<SmartHealthMonitoring.Models.Patient>> GetPatientsAsync(string search, int page, int pageSize)
        {
            int totalRecords = await _repository.GetPatientsCountAsync(search);
            var patients = await _repository.GetPatientsAsync(search, page, pageSize);

            return new PagedResult<global::SmartHealthMonitoring.Models.Patient>
            {
                Items = patients,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<SmartHealthMonitoring.Models.Patient?> GetPatientDetailsAsync(int id)
        {
            return await _repository.GetPatientByIdAsync(id);
        }

        public async Task<(bool success, string message)> RegisterPatientAsync(ReceptionistRegisterPatientViewModel model)
        {
            bool emailExists = await _repository.EmailExistsAsync(model.Email);
            bool phoneExists = await _repository.PhoneExistsAsync(model.Phone);
            bool citizenIdExists = await _repository.CitizenIdExistsAsync(model.CitizenId);

            if (emailExists) return (false, "Email n�y d� du?c s? d?ng trong h? th?ng.");
            if (phoneExists) return (false, "S? di?n tho?i n�y d� du?c s? d?ng trong h? th?ng.");
            if (citizenIdExists) return (false, "CCCD n�y d� du?c s? d?ng trong h? th?ng.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var minDate = new DateOnly(1900, 1, 1);

            if (model.DateOfBirth < minDate)
                return (false, $"Ng�y sinh kh�ng h?p l?. Nam sinh ph?i t? {minDate.Year} tr? di.");

            if (model.DateOfBirth > today)
                return (false, "Ng�y sinh kh�ng h?p l?. Ng�y sinh kh�ng du?c l?n hon ng�y hi?n t?i.");

            int age = today.Year - model.DateOfBirth.Year;
            if (model.DateOfBirth > today.AddYears(-age)) age--;

            if (age > 150)
                return (false, "Ng�y sinh kh�ng h?p l?. Tu?i kh�ng du?c vu?t qu� 150.");

            string randomPassword = GenerateRandomPassword(8);
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);

            var context = _repository.GetContext();
            var strategy = context.Database.CreateExecutionStrategy();

            bool success = false;
            string message = "";

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var user = new User
                    {
                        FullName = model.FullName,
                        Email = model.Email,
                        PasswordHash = passwordHash,
                        Role = 0,
                        IsDeleted = false,
                        CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
                    };

                    await _repository.AddUserAsync(user);

                    var patient = new global::SmartHealthMonitoring.Models.Patient
                    {
                        UserId = user.Id,
                        DateOfBirth = model.DateOfBirth,
                        Sex = model.Sex,
                        Phone = model.Phone,
                        Address = model.Address,
                        CitizenId = model.CitizenId,
                        IsDeleted = false
                    };

                    await _repository.AddPatientAsync(patient);
                    await transaction.CommitAsync();
                    success = true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    success = false;
                    message = "�� x?y ra l?i khi dang k� b?nh nh�n: " + ex.Message;
                }
            });

            if (!success) return (false, message);

            var replacements = new Dictionary<string, string>
            {
                { "{{FullName}}", model.FullName },
                { "{{Email}}", model.Email },
                { "{{Password}}", randomPassword }
            };

            var htmlContent = _emailService.GetHtmlContentFromFile("NewPatientAccount.html", replacements);
            if (string.IsNullOrEmpty(htmlContent))
            {
                htmlContent = $@"
                    <h2>Xin ch�o {model.FullName},</h2>
                    <p>H? so b?nh nh�n c?a b?n d� du?c dang k� th�nh c�ng t?i SmartHealth.</p>
                    <p>Th�ng tin t�i kho?n c?a b?n d? dang nh?p v�o h? th?ng:</p>
                    <ul>
                        <li><strong>Email:</strong> {model.Email}</li>
                        <li><strong>M?t kh?u:</strong> {randomPassword}</li>
                    </ul>
                    <p>Vui l�ng dang nh?p v� d?i m?t kh?u s?m nh?t c� th? d? d?m b?o b?o m?t.</p>
                    <p>Tr�n tr?ng,</p>
                    <p>SmartHealth Clinic</p>";
            }

            await _emailService.SendEmailAsync(model.Email, "T�i kho?n b?nh nh�n - SmartHealth Clinic", htmlContent);

            return (true, "�ang k� b?nh nh�n th�nh c�ng. M?t kh?u d� du?c g?i qua email.");
        }

        public async Task<(bool success, string message)> AddToWaitingListAsync(int patientId, int doctorId, int slotId, int receptionistId)
        {
            var patient = await _repository.GetPatientByIdAsync(patientId);
            if (patient == null) return (false, "B?nh nh�n kh�ng t?n t?i.");

            var isActiveSession = await _repository.IsPatientInWaitingListAsync(patientId);
            if (isActiveSession) return (false, "B?nh nh�n dang trong danh s�ch ch? ho?c dang du?c b�c si kh�m.");

            var slot = await _repository.GetAvailableSlotAsync(slotId, doctorId);
            if (slot == null) return (false, "Slot kh�m d� du?c d?t ho?c kh�ng c�n h?p l?. Vui l�ng ch?n l?i.");

            var context = _repository.GetContext();
            var strategy = context.Database.CreateExecutionStrategy();
            
            bool success = false;
            string message = "";

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var freshSlot = await _repository.GetAvailableSlotAsync(slotId, doctorId);
                    if (freshSlot == null)
                    {
                        message = "Slot kh�m d� du?c d?t ho?c kh�ng c�n h?p l?. Vui l�ng ch?n l?i.";
                        await transaction.RollbackAsync();
                        return;
                    }

                    freshSlot.Status = AppointmentSlotStatus.Booked;
                    freshSlot.PatientId = patientId;

                    var appointment = new global::SmartHealthMonitoring.Models.Appointment
                    {
                        SlotId = slotId,
                        PatientId = patientId,
                        DoctorId = doctorId,
                        Status = AppointmentStatus.Confirmed,
                        PatientNote = "�ang k� tr?c ti?p t?i qu?y l? t�n",
                        CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
                    };
                    await _repository.AddAppointmentAsync(appointment);

                    var today = SmartHealthMonitoring.Common.AppTime.Now.Date;
                    var currentMaxSeq = await _repository.GetMaxSequenceNumberTodayAsync(doctorId, today);
                    var newSeq = currentMaxSeq + 1;

                    var waitingPatient = new WaitingPatient
                    {
                        PatientId = patientId,
                        ReceptionistId = receptionistId,
                        DoctorId = doctorId,
                        SequenceNumber = newSeq,
                        Status = 0,
                        CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
                    };

                    await _repository.AddWaitingPatientAsync(waitingPatient);
                    await transaction.CommitAsync();

                    success = true;
                    message = $"�� dang k� kh�m cho b?nh nh�n {patient.User?.FullName ?? ""}. S? th? t?: {newSeq}";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    success = false;
                    message = "�� x?y ra l?i khi dang k� kh�m: " + ex.Message;
                }
            });

            return (success, message);
        }

        public async Task<List<dynamic>> GetAvailableDoctorsAsync()
        {
            var nowVn       = SmartHealthMonitoring.Common.AppTime.Now;
            var todayVn     = nowVn.Date;
            var tomorrowVn  = todayVn.AddDays(1);

            return await _repository.GetDoctorsWithSlotsAsync(nowVn, tomorrowVn);
        }

        public async Task<List<dynamic>> GetDoctorSlotsAsync(int doctorId)
        {
            var nowVn      = SmartHealthMonitoring.Common.AppTime.Now;
            var tomorrowVn = nowVn.Date.AddDays(1);

            return await _repository.GetDoctorSlotsAsync(doctorId, nowVn, tomorrowVn);
        }

        private async Task GenerateSlotsForDateAsync(DateTime localDate, TimeZoneInfo vnZone)
        {
            int dayOfWeek = (int)localDate.DayOfWeek;
            var schedules = await _repository.GetWorkSchedulesByDayAsync(dayOfWeek);

            if (!schedules.Any()) return;

            int created = 0;
            foreach (var schedule in schedules)
            {
                var current = schedule.StartTime;
                while (current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
                {
                    var slotStartLocal = localDate.Add(current.ToTimeSpan());
                    var slotStartUtc   = TimeZoneInfo.ConvertTimeToUtc(slotStartLocal, vnZone);
                    var slotEndUtc     = slotStartUtc.AddMinutes(schedule.SlotDurationMinutes);

                    bool exists = await _repository.SlotExistsAsync(schedule.DoctorId, slotStartUtc);

                    if (!exists)
                    {
                        var slot = new global::SmartHealthMonitoring.Models.AppointmentSlot
                        {
                            DoctorId  = schedule.DoctorId,
                            SlotStart = slotStartUtc,
                            SlotEnd   = slotEndUtc,
                            Status    = AppointmentSlotStatus.Available
                        };
                        await _repository.AddAppointmentSlotAsync(slot);
                        created++;
                    }

                    current = current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));
                }
            }

            if (created > 0)
                await _repository.SaveChangesAsync();
        }

        private string GenerateRandomPassword(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}



