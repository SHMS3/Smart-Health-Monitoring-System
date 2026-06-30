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
    [Authorize(Roles = "2,3")] // Admin and Receptionist
    public class ReceptionistController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;

        // ─── Cấu hình VietQR của phòng khám ───────────────────────────────────────
        // Thay bằng thông tin ngân hàng thực của phòng khám
        private const string BANK_ID = "MB";          // Mã ngân hàng (MBBank)
        private const string ACCOUNT_NO = "1508200456788";  // Số tài khoản
        private const string ACCOUNT_NAME = "PHAM THE SON"; // Tên chủ TK
        // ────────────────────────────────────────────────────────────────────────────

        public ReceptionistController(SmartHealthMonitoringContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: /Receptionist/Index – Danh sách phiếu thanh toán
        public async Task<IActionResult> Index(string status = "All", int page = 1, int pageSize = 10)
        {
            var query = _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (status != "All")
            {
                query = query.Where(p => p.Status == status);
            }

            int totalRecords = await query.CountAsync();

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentStatus = status;

            var result = new PagedResult<Payment>
            {
                Items = payments,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            return View(result);
        }

        // GET: /Receptionist/Details/5 – Xem chi tiết phiếu (read-only)
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

        // GET: /Receptionist/Checkout/5 – Trang thanh toán hóa đơn
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

            // Tạo nội dung chuyển khoản cố định theo mã phiếu
            var transferContent = $"THANHTOAN HD{payment.Id:D5}";

            ViewBag.BankId = BANK_ID;
            ViewBag.AccountNo = ACCOUNT_NO;
            ViewBag.AccountName = ACCOUNT_NAME;
            ViewBag.TransferContent = transferContent;

            // Build VietQR image URL (quick link, không cần API key)
            var vietQrUrl = $"https://img.vietqr.io/image/{BANK_ID}-{ACCOUNT_NO}-compact2.png" +
                            $"?amount={payment.TotalAmount:F0}" +
                            $"&addInfo={Uri.EscapeDataString(transferContent)}" +
                            $"&accountName={Uri.EscapeDataString(ACCOUNT_NAME)}";
            ViewBag.VietQrUrl = vietQrUrl;

            return View(payment);
        }

        // POST: /Receptionist/ConfirmCash – Xác nhận thanh toán tiền mặt
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

        // GET: /Receptionist/Patients – Danh sách bệnh nhân
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

        // GET: /Receptionist/PatientDetails/5
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

        // GET: /Receptionist/RegisterPatient
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

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng trong hệ thống.");
                return View(model);
            }

            // Generate random password
            string randomPassword = GenerateRandomPassword(8);
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);

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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi đăng ký bệnh nhân: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToWaitingList(int patientId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int receptionistId))
            {
                TempData["Error"] = "Không thể xác định thông tin tài khoản lễ tân.";
                return RedirectToAction(nameof(Patients));
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);
            if (patient == null)
            {
                TempData["Error"] = "Bệnh nhân không tồn tại.";
                return RedirectToAction(nameof(Patients));
            }

            // Check if patient is already in the queue waiting or being examined
            var isActiveSession = await _context.WaitingPatients
                .AnyAsync(w => w.PatientId == patientId && (w.Status == 0 || w.Status == 1));

            if (isActiveSession)
            {
                TempData["Error"] = "Bệnh nhân đang trong danh sách chờ hoặc đang được bác sĩ khám.";
                return RedirectToAction(nameof(Patients));
            }

            // Generate sequence number for today
            var today = DateTime.UtcNow.Date;
            var currentMaxSeq = await _context.WaitingPatients
                .Where(w => w.CreatedAt >= today)
                .MaxAsync(w => (int?)w.SequenceNumber) ?? 0;

            var newSeq = currentMaxSeq + 1;

            var waitingPatient = new WaitingPatient
            {
                PatientId = patientId,
                ReceptionistId = receptionistId,
                SequenceNumber = newSeq,
                Status = 0, // Waiting
                CreatedAt = DateTime.UtcNow
            };

            _context.WaitingPatients.Add(waitingPatient);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm bệnh nhân {patient.User?.FullName ?? ""} vào danh sách chờ khám. Số thứ tự: {newSeq}";
            return RedirectToAction(nameof(Patients));
        }

        private string GenerateRandomPassword(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
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
