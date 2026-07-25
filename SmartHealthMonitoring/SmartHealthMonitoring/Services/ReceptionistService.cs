using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services
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
                return (false, "Không tìm thấy phiếu thanh toán");

            if (payment.Status != "Pending")
                return (false, "Phiếu này đã được xử lý");

            payment.Status = "Paid";
            payment.PaidAt = DateTime.UtcNow;
            payment.PaymentMethod = "Cash";

            await _repository.UpdatePaymentAsync(payment);
            return (true, "Xác nhận thanh toán tiền mặt thành công!");
        }

        public async Task<(bool paid, string message)> CheckQrPaymentStatusAsync(int id)
        {
            var payment = await _repository.GetPaymentByIdAsync(id);
            if (payment == null)
                return (false, "Không tìm thấy phiếu");

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
                    pmt.PaidAt = DateTime.UtcNow;
                    pmt.PaymentMethod = "QR";
                    await _repository.UpdatePaymentAsync(pmt);
                    return (true, "Success", pmt.Id);
                }
            }

            return (false, "Không tìm thấy phiếu phù hợp", null);
        }

        public async Task<PagedResult<Patient>> GetPatientsAsync(string search, int page, int pageSize)
        {
            int totalRecords = await _repository.GetPatientsCountAsync(search);
            var patients = await _repository.GetPatientsAsync(search, page, pageSize);

            return new PagedResult<Patient>
            {
                Items = patients,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<Patient?> GetPatientDetailsAsync(int id)
        {
            return await _repository.GetPatientByIdAsync(id);
        }

        public async Task<(bool success, string message)> RegisterPatientAsync(ReceptionistRegisterPatientViewModel model)
        {
            bool emailExists = await _repository.EmailExistsAsync(model.Email);
            bool phoneExists = await _repository.PhoneExistsAsync(model.Phone);
            bool citizenIdExists = await _repository.CitizenIdExistsAsync(model.CitizenId);

            if (emailExists) return (false, "Email này đã được sử dụng trong hệ thống.");
            if (phoneExists) return (false, "Số điện thoại này đã được sử dụng trong hệ thống.");
            if (citizenIdExists) return (false, "CCCD này đã được sử dụng trong hệ thống.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var minDate = new DateOnly(1900, 1, 1);

            if (model.DateOfBirth < minDate)
                return (false, $"Ngày sinh không hợp lệ. Năm sinh phải từ {minDate.Year} trở đi.");

            if (model.DateOfBirth > today)
                return (false, "Ngày sinh không hợp lệ. Ngày sinh không được lớn hơn ngày hiện tại.");

            int age = today.Year - model.DateOfBirth.Year;
            if (model.DateOfBirth > today.AddYears(-age)) age--;

            if (age > 150)
                return (false, "Ngày sinh không hợp lệ. Tuổi không được vượt quá 150.");

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
                        CreatedAt = DateTime.UtcNow
                    };

                    await _repository.AddUserAsync(user);

                    var patient = new Patient
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
                    message = "Đã xảy ra lỗi khi đăng ký bệnh nhân: " + ex.Message;
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
                    <h2>Xin chào {model.FullName},</h2>
                    <p>Hồ sơ bệnh nhân của bạn đã được đăng ký thành công tại SmartHealth.</p>
                    <p>Thông tin tài khoản của bạn để đăng nhập vào hệ thống:</p>
                    <ul>
                        <li><strong>Email:</strong> {model.Email}</li>
                        <li><strong>Mật khẩu:</strong> {randomPassword}</li>
                    </ul>
                    <p>Vui lòng đăng nhập và đổi mật khẩu sớm nhất có thể để đảm bảo bảo mật.</p>
                    <p>Trân trọng,</p>
                    <p>SmartHealth Clinic</p>";
            }

            await _emailService.SendEmailAsync(model.Email, "Tài khoản bệnh nhân - SmartHealth Clinic", htmlContent);

            return (true, "Đăng ký bệnh nhân thành công. Mật khẩu đã được gửi qua email.");
        }

        public async Task<(bool success, string message)> AddToWaitingListAsync(int patientId, int doctorId, int slotId, int receptionistId)
        {
            var patient = await _repository.GetPatientByIdAsync(patientId);
            if (patient == null) return (false, "Bệnh nhân không tồn tại.");

            var isActiveSession = await _repository.IsPatientInWaitingListAsync(patientId);
            if (isActiveSession) return (false, "Bệnh nhân đang trong danh sách chờ hoặc đang được bác sĩ khám.");

            var slot = await _repository.GetAvailableSlotAsync(slotId, doctorId);
            if (slot == null) return (false, "Slot khám đã được đặt hoặc không còn hợp lệ. Vui lòng chọn lại.");

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
                        message = "Slot khám đã được đặt hoặc không còn hợp lệ. Vui lòng chọn lại.";
                        await transaction.RollbackAsync();
                        return;
                    }

                    freshSlot.Status = AppointmentSlotStatus.Booked;
                    freshSlot.PatientId = patientId;

                    var appointment = new Appointment
                    {
                        SlotId = slotId,
                        PatientId = patientId,
                        DoctorId = doctorId,
                        Status = AppointmentStatus.Confirmed,
                        PatientNote = "Đăng ký trực tiếp tại quầy lễ tân",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _repository.AddAppointmentAsync(appointment);

                    var today = DateTime.UtcNow.Date;
                    var currentMaxSeq = await _repository.GetMaxSequenceNumberTodayAsync(doctorId, today);
                    var newSeq = currentMaxSeq + 1;

                    var waitingPatient = new WaitingPatient
                    {
                        PatientId = patientId,
                        ReceptionistId = receptionistId,
                        DoctorId = doctorId,
                        SequenceNumber = newSeq,
                        Status = 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _repository.AddWaitingPatientAsync(waitingPatient);
                    await transaction.CommitAsync();

                    success = true;
                    message = $"Đã đăng ký khám cho bệnh nhân {patient.User?.FullName ?? ""}. Số thứ tự: {newSeq}";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    success = false;
                    message = "Đã xảy ra lỗi khi đăng ký khám: " + ex.Message;
                }
            });

            return (success, message);
        }

        public async Task<List<dynamic>> GetAvailableDoctorsAsync()
        {
            var vnZone   = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnZone);
            var todayVn  = nowVn.Date;
            var todayUtc    = TimeZoneInfo.ConvertTimeToUtc(todayVn, vnZone);
            var tomorrowUtc = TimeZoneInfo.ConvertTimeToUtc(todayVn.AddDays(1), vnZone);

            bool hasSlots = await _repository.HasSlotsForDateAsync(todayUtc, tomorrowUtc);

            if (!hasSlots)
            {
                await GenerateSlotsForDateAsync(todayVn, vnZone);
            }

            return await _repository.GetDoctorsWithSlotsAsync(todayUtc, tomorrowUtc);
        }

        public async Task<List<dynamic>> GetDoctorSlotsAsync(int doctorId)
        {
            var vnZone   = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnZone);
            var todayVn  = nowVn.Date;
            var tomorrowUtc = TimeZoneInfo.ConvertTimeToUtc(todayVn.AddDays(1), vnZone);
            var nowUtc = DateTime.UtcNow;

            return await _repository.GetDoctorSlotsAsync(doctorId, nowUtc, tomorrowUtc);
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
                        var slot = new AppointmentSlot
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
