using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "2,3")] 
    public class ReceptionistController : Controller
    {
        private const string BANK_ID = "MB";          // Mã ngân hàng (MBBank)
        private const string ACCOUNT_NO = "1508200456788";  // Số tài khoản
        private const string ACCOUNT_NAME = "PHAM THE SON"; // Tên chủ TK
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;
        private readonly IAppointmentService _appointmentService;
        private readonly IEmailTriggerService _emailTriggerService;

        public ReceptionistController(
            SmartHealthMonitoringContext context,
            IEmailService emailService,
            IAppointmentService appointmentService,
            IEmailTriggerService emailTriggerService)
        {
            _context = context;
            _emailService = emailService;
            _appointmentService = appointmentService;
            _emailTriggerService = emailTriggerService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var query = _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Where(p => p.Status == "Pending");

            int totalRecords = await query.CountAsync();

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<Payment>
            {
                Items = payments,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            return View(result);
        }

        public async Task<IActionResult> PaymentHistory(DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10)
        {
            if (!fromDate.HasValue) fromDate = DateTime.Today;
            if (!toDate.HasValue) toDate = DateTime.Today;

            var query = _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Where(p => p.Status == "Paid");

            var start = fromDate.Value.Date;
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);

            query = query.Where(p => p.CreatedAt >= start && p.CreatedAt <= end);

            int totalRecords = await query.CountAsync();

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.FromDate = start.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

            var result = new PagedResult<Payment>
            {
                Items = payments,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            return View(result);
        }

        public async Task<IActionResult> Details(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Include(p => p.PaymentDetails).ThenInclude(pd => pd.Service)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null) return NotFound();
            return View(payment);
        }

        public async Task<IActionResult> Checkout(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Include(p => p.PaymentDetails).ThenInclude(pd => pd.Service)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null) return NotFound();
            if (payment.Status != "Pending")
            {
                TempData["Error"] = "Phiếu thanh toán này đã được xử lý.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var transferContent = $"THANHTOAN HD{payment.Id:D5}";

            ViewBag.BankId = BANK_ID;
            ViewBag.AccountNo = ACCOUNT_NO;
            ViewBag.AccountName = ACCOUNT_NAME;
            ViewBag.TransferContent = transferContent;

            var vietQrUrl = $"https://img.vietqr.io/image/{BANK_ID}-{ACCOUNT_NO}-compact2.png" +
                            $"?amount={payment.TotalAmount:F0}" +
                            $"&addInfo={Uri.EscapeDataString(transferContent)}" +
                            $"&accountName={Uri.EscapeDataString(ACCOUNT_NAME)}";
            ViewBag.VietQrUrl = vietQrUrl;

            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCash(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
                return Json(new { success = false, message = "Không tìm thấy phiếu thanh toán" });

            if (payment.Status != "Pending")
                return Json(new { success = false, message = "Phiếu này đã được xử lý" });

            payment.Status = "Paid";
            payment.PaidAt = DateTime.UtcNow;
            payment.PaymentMethod = "Cash";

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xác nhận thanh toán tiền mặt thành công!" });
        }

        // POST: /Receptionist/ConfirmPayment – Giữ lại tương thích cũ (dùng bởi Details view)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            return await ConfirmCash(id);
        }

        // GET: /Receptionist/CheckQrPayment?id=5&content=... – Polling kiểm tra SePay webhook
        // SePay sẽ gọi webhook về server (endpoint riêng) và cập nhật DB.
        // Client JS poll endpoint này mỗi 3 giây để biết trạng thái.
        [HttpGet]
        public async Task<IActionResult> CheckQrPayment(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
                return Json(new { paid = false, message = "Không tìm thấy phiếu" });

            return Json(new { paid = payment.Status == "Paid", message = payment.Status });
        }

        // POST: /Receptionist/SepayWebhook – Nhận webhook từ SePay
        // SePay gửi POST khi có giao dịch khớp nội dung
        [HttpPost]
        [AllowAnonymous] // Webhook từ SePay không mang cookie auth
        [IgnoreAntiforgeryToken] // Bỏ qua check CSRF token cho webhook
        public async Task<IActionResult> SepayWebhook([FromBody] SepayWebhookPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Content))
            {
                Console.WriteLine("SepayWebhook: Payload is null or Content is empty.");
                return Ok(new { success = false, message = "Empty payload" });
            }

            Console.WriteLine($"SepayWebhook RECEIVED: Amount={payload.TransferAmount}, Content={payload.Content}");

            // Tìm phiếu theo nội dung chuyển khoản "THANHTOAN HD00001"
            var content = payload.Content.ToUpper();
            var payments = await _context.Payments
                .Where(p => p.Status == "Pending")
                .ToListAsync();

            foreach (var pmt in payments)
            {
                var expectedContent = $"THANHTOAN HD{pmt.Id:D5}";
                if (content.Contains(expectedContent.ToUpper()))
                {
                    pmt.Status = "Paid";
                    pmt.PaidAt = DateTime.UtcNow;
                    pmt.PaymentMethod = "QR";
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, paymentId = pmt.Id });
                }
            }

            return Ok(new { success = false, message = "Không tìm thấy phiếu phù hợp" });
        }

        public async Task<IActionResult> Patients(string search, int page = 1, int pageSize = 10)
        {
            var query = _context.Patients
                .Include(p => p.User)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(p =>
                    (p.User.FullName != null && p.User.FullName.ToLower().Contains(lowerSearch)) ||
                    (p.Phone != null && p.Phone.Contains(search)) ||
                    (p.User.Email != null && p.User.Email.ToLower().Contains(lowerSearch)) ||
                    (p.CitizenId != null && p.CitizenId.Contains(search))
                );
            }

            int totalRecords = await query.CountAsync();

            var patients = await query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<Patient>
            {
                Items = patients,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            var viewModel = new ReceptionistPatientListViewModel
            {
                Patients = result,
                SearchQuery = search
            };

            return View(viewModel);
        }

        public async Task<IActionResult> PatientDetails(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (patient == null)
            {
                return NotFound();
            }

            return View(patient);
        }

        public IActionResult RegisterPatient()
        {
            return View(new ReceptionistRegisterPatientViewModel());
        }

        // POST: /Receptionist/RegisterPatient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPatient(ReceptionistRegisterPatientViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == model.Email && !u.IsDeleted);

            bool phoneExists = await _context.Users
     .AnyAsync(u => u.Patients.Any(p => p.Phone == model.Phone) ||
                    u.Doctors.Any(d => d.Phone == model.Phone));

            bool citizenId = await _context.Users
   .AnyAsync(u => u.Patients.Any(p => p.CitizenId == model.CitizenId) ||
                  u.Doctors.Any(d => d.CitizenId == model.CitizenId));

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng trong hệ thống.");
                return View(model);
            }

            if (phoneExists)
            {
                ModelState.AddModelError("Phone", "Số điện thoại này đã được sử dụng trong hệ thống.");
                return View(model);
            }

            if (citizenId)
            {
                ModelState.AddModelError("CitizenId", "CCCD này đã được sử dụng trong hệ thống.");
                return View(model);
            }

            if (model.DateOfBirth > DateOnly.FromDateTime(DateTime.Now))
            {
                ModelState.AddModelError("DateOfBirth", "Ngày sinh không được lớn hơn ngày hiện tại.");
                return View(model);
            }

            // Generate random password
            string randomPassword = GenerateRandomPassword(8);
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var user = new User
                        {
                            FullName = model.FullName,
                            Email = model.Email,
                            PasswordHash = passwordHash,
                            Role = 0, // Patient Role
                            IsDeleted = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();

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

                        _context.Patients.Add(patient);
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();

                        // Send email to patient
                        var replacements = new Dictionary<string, string>
                        {
                            { "{{FullName}}", model.FullName },
                            { "{{Email}}", model.Email },
                            { "{{Password}}", randomPassword }
                        };

                        var htmlContent = _emailService.GetHtmlContentFromFile("NewPatientAccount.html", replacements);
                        if (string.IsNullOrEmpty(htmlContent))
                        {
                            // Fallback content if template is missing
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

                        TempData["Success"] = "Đăng ký bệnh nhân thành công. Mật khẩu đã được gửi qua email.";
                        return RedirectToAction(nameof(Patients));
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi đăng ký bệnh nhân: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToWaitingList(int patientId, int doctorId, int slotId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int receptionistId))
            {
                TempData["Error"] = "Không thể xác định thông tin tài khoản lễ tân.";
                return RedirectToAction(nameof(Patients));
            }

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);
            if (patient == null)
            {
                TempData["Error"] = "Bệnh nhân không tồn tại.";
                return RedirectToAction(nameof(Patients));
            }

            // Kiểm tra bệnh nhân đã trong danh sách chờ chưa
            var isActiveSession = await _context.WaitingPatients
                .AnyAsync(w => w.PatientId == patientId && (w.Status == 0 || w.Status == 1));

            if (isActiveSession)
            {
                TempData["Error"] = "Bệnh nhân đang trong danh sách chờ hoặc đang được bác sĩ khám.";
                return RedirectToAction(nameof(Patients));
            }

            // Lấy slot và kiểm tra còn Available không
            var slot = await _context.AppointmentSlots
                .FirstOrDefaultAsync(s => s.Id == slotId && s.DoctorId == doctorId && s.Status == AppointmentSlotStatus.Available);

            if (slot == null)
            {
                TempData["Error"] = "Slot khám đã được đặt hoặc không còn hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Patients));
            }

            // Dùng ExecutionStrategy để tương thích với SqlServerRetryingExecutionStrategy
            var strategy = _context.Database.CreateExecutionStrategy();
            string? successMsg = null;
            string? errorMsg = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Re-check slot vẫn Available (tránh race condition)
                    var freshSlot = await _context.AppointmentSlots
                        .FirstOrDefaultAsync(s => s.Id == slotId && s.DoctorId == doctorId && s.Status == AppointmentSlotStatus.Available);

                    if (freshSlot == null)
                    {
                        errorMsg = "Slot khám đã được đặt hoặc không còn hợp lệ. Vui lòng chọn lại.";
                        await transaction.RollbackAsync();
                        return;
                    }

                    // Book slot
                    freshSlot.Status = AppointmentSlotStatus.Booked;
                    freshSlot.PatientId = patientId;

                    // Tạo Appointment
                    var appointment = new Appointment
                    {
                        SlotId = slotId,
                        PatientId = patientId,
                        DoctorId = doctorId,
                        Status = AppointmentStatus.Confirmed,
                        PatientNote = "Đăng ký trực tiếp tại quầy lễ tân",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Appointments.Add(appointment);
                    await _context.SaveChangesAsync();

                    // Tạo số thứ tự cho bác sĩ cụ thể hôm nay (chỉ tính các bản ghi chưa hủy)
                    var today = DateTime.UtcNow.Date;
                    var currentMaxSeq = await _context.WaitingPatients
                        .Where(w => w.CreatedAt >= today && w.DoctorId == doctorId && w.Status != 2)
                        .MaxAsync(w => (int?)w.SequenceNumber) ?? 0;

                    var newSeq = currentMaxSeq + 1;

                    // Tạo WaitingPatient với DoctorId đã xác định
                    var waitingPatient = new WaitingPatient
                    {
                        PatientId = patientId,
                        ReceptionistId = receptionistId,
                        DoctorId = doctorId,
                        SequenceNumber = newSeq,
                        Status = 0, // Đang chờ
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.WaitingPatients.Add(waitingPatient);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    successMsg = $"Đã đăng ký khám cho bệnh nhân {patient.User?.FullName ?? ""}. Số thứ tự: {newSeq}";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    errorMsg = "Đã xảy ra lỗi khi đăng ký khám: " + ex.Message;
                }
            });

            if (errorMsg != null) TempData["Error"] = errorMsg;
            if (successMsg != null) TempData["Success"] = successMsg;

            return RedirectToAction(nameof(Patients));
        }

        // GET: /Receptionist/GetAvailableDoctors – Lấy danh sách bác sĩ có slot Available hôm nay
        [HttpGet]
        public async Task<IActionResult> GetAvailableDoctors()
        {
            // Dùng múi giờ Việt Nam (UTC+7) để xác định ngày hôm nay
            var vnZone   = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnZone);
            var todayVn  = nowVn.Date;
            // Slot được lưu theo UTC trong DB
            var todayUtc    = TimeZoneInfo.ConvertTimeToUtc(todayVn, vnZone);
            var tomorrowUtc = TimeZoneInfo.ConvertTimeToUtc(todayVn.AddDays(1), vnZone);

            // Nếu chưa có slot nào cho hôm nay → generate on-the-fly từ DoctorWorkSchedules
            bool hasSlots = await _context.AppointmentSlots
                .AnyAsync(s => s.SlotStart >= todayUtc && s.SlotStart < tomorrowUtc);

            if (!hasSlots)
            {
                await GenerateSlotsForDateAsync(todayVn, vnZone);
            }

            var doctors = await _context.AppointmentSlots
                .Where(s => s.Status == AppointmentSlotStatus.Available
                         && s.SlotStart >= todayUtc
                         && s.SlotStart < tomorrowUtc)
                .Include(s => s.Doctor).ThenInclude(d => d.User)
                .GroupBy(s => s.DoctorId)
                .Select(g => new
                {
                    doctorId = g.Key,
                    doctorName = g.First().Doctor.User.FullName,
                    specialty = g.First().Doctor.Specialty,
                    roomNumber = g.First().Doctor.RoomNumber,
                    availableSlots = g.Count()
                })
                .OrderBy(d => d.doctorName)
                .ToListAsync();

            return Json(new { success = true, data = doctors });
        }

        // GET: /Receptionist/GetDoctorSlots?doctorId=X – Lấy slot Available hôm nay của bác sĩ
        [HttpGet]
        public async Task<IActionResult> GetDoctorSlots(int doctorId)
        {
            var vnZone   = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnZone);
            var todayVn  = nowVn.Date;
            var todayUtc    = TimeZoneInfo.ConvertTimeToUtc(todayVn, vnZone);
            var tomorrowUtc = TimeZoneInfo.ConvertTimeToUtc(todayVn.AddDays(1), vnZone);
            // Chỉ lấy slot từ thời điểm hiện tại trở đi (không hiện slot đã qua)
            var nowUtc = DateTime.UtcNow;

            var slots = await _context.AppointmentSlots
                .Where(s => s.DoctorId == doctorId
                         && s.Status == AppointmentSlotStatus.Available
                         && s.SlotStart >= nowUtc
                         && s.SlotStart < tomorrowUtc)
                .OrderBy(s => s.SlotStart)
                .Select(s => new
                {
                    slotId = s.Id,
                    slotStart = s.SlotStart,
                    slotEnd = s.SlotEnd
                })
                .ToListAsync();

            return Json(new { success = true, data = slots });
        }

        private async Task GenerateSlotsForDateAsync(DateTime localDate, TimeZoneInfo vnZone)
        {
            // DayOfWeek trong DB: 0=CN,1=T2,...,6=T7 – khớp với System.DayOfWeek (Sunday=0)
            int dayOfWeek = (int)localDate.DayOfWeek;

            var schedules = await _context.DoctorWorkSchedules
                .Where(s => s.IsActive && s.DayOfWeek == dayOfWeek)
                .ToListAsync();

            if (!schedules.Any()) return;

            int created = 0;
            foreach (var schedule in schedules)
            {
                var current = schedule.StartTime;
                while (current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
                {
                    // Slot lưu theo UTC
                    var slotStartLocal = localDate.Add(current.ToTimeSpan());
                    var slotStartUtc   = TimeZoneInfo.ConvertTimeToUtc(slotStartLocal, vnZone);
                    var slotEndUtc     = slotStartUtc.AddMinutes(schedule.SlotDurationMinutes);

                    bool exists = await _context.AppointmentSlots
                        .AnyAsync(s => s.DoctorId == schedule.DoctorId && s.SlotStart == slotStartUtc);

                    if (!exists)
                    {
                        _context.AppointmentSlots.Add(new AppointmentSlot
                        {
                            DoctorId  = schedule.DoctorId,
                            SlotStart = slotStartUtc,
                            SlotEnd   = slotEndUtc,
                            Status    = AppointmentSlotStatus.Available
                        });
                        created++;
                    }

                    current = current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));
                }
            }

            if (created > 0)
                await _context.SaveChangesAsync();
        }

        private string GenerateRandomPassword(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // GET: /Receptionist/PendingAppointments
        [HttpGet]
        public async Task<IActionResult> PendingAppointments()
        {
            var pendingList = await _appointmentService.GetPendingAppointmentsAsync();
            return View(pendingList);
        }

        // POST: /Receptionist/ApproveBooking  (BOOK-08)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int appointmentId)
        {
            var success = await _appointmentService.ApproveAppointmentBookingAsync(appointmentId);
            if (success)
            {
                // NTF-01: Email xác nhận đặt lịch + QR Check-in
                try
                {
                    await _emailTriggerService.SendBookingConfirmationCheckInAsync(appointmentId);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"[ApproveBooking Email] {emailEx.Message}");
                }

                TempData["Success"] = "Đã phê duyệt yêu cầu đặt lịch hẹn thành công.";
            }
            else
            {
                TempData["Error"] = "Phê duyệt thất bại. Lịch hẹn không hợp lệ hoặc đã được xử lý.";
            }
            return RedirectToAction(nameof(PendingAppointments));
        }

        // POST: /Receptionist/RejectBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int appointmentId)
        {
            var success = await _appointmentService.RejectAppointmentBookingAsync(appointmentId);
            if (success)
            {
                TempData["Success"] = "Đã từ chối yêu cầu đặt lịch hẹn.";
            }
            else
            {
                TempData["Error"] = "Từ chối thất bại.";
            }
            return RedirectToAction(nameof(PendingAppointments));
        }

        // POST: /Receptionist/ApproveCancellation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCancellation(int appointmentId)
        {
            var success = await _appointmentService.ApproveAppointmentCancellationAsync(appointmentId);
            if (success)
            {
                TempData["Success"] = "Đã đồng ý hủy lịch hẹn thành công.";
            }
            else
            {
                TempData["Error"] = "Phê duyệt hủy lịch thất bại.";
            }
            return RedirectToAction(nameof(PendingAppointments));
        }

        // POST: /Receptionist/RejectCancellation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCancellation(int appointmentId)
        {
            var success = await _appointmentService.RejectAppointmentCancellationAsync(appointmentId);
            if (success)
            {
                TempData["Success"] = "Đã bác bỏ yêu cầu hủy lịch hẹn.";
            }
            else
            {
                TempData["Error"] = "Từ chối yêu cầu hủy thất bại.";
            }
            return RedirectToAction(nameof(PendingAppointments));
        }
    }
    // DTO cho SePay webhook
    public class SepayWebhookPayload
    {
        public string? Content      { get; set; }
        public decimal? TransferAmount { get; set; }
        public string? ReferenceCode  { get; set; }
    }
}
