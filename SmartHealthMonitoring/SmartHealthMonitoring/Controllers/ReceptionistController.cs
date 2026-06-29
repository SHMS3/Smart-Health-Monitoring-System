using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "2,3")] // Admin and Receptionist
    public class ReceptionistController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        // ─── Cấu hình VietQR của phòng khám ───────────────────────────────────────
        // Thay bằng thông tin ngân hàng thực của phòng khám
        private const string BANK_ID      = "MB";          // Mã ngân hàng (MBBank)
        private const string ACCOUNT_NO   = "1508200456788";  // Số tài khoản
        private const string ACCOUNT_NAME = "PHAM THE SON"; // Tên chủ TK
        // ────────────────────────────────────────────────────────────────────────────

        public ReceptionistController(SmartHealthMonitoringContext context)
        {
            _context = context;
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

            ViewBag.BankId        = BANK_ID;
            ViewBag.AccountNo     = ACCOUNT_NO;
            ViewBag.AccountName   = ACCOUNT_NAME;
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

            payment.Status        = "Paid";
            payment.PaidAt        = DateTime.UtcNow;
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
        public async Task<IActionResult> SepayWebhook([FromBody] SepayWebhookPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Content))
                return Ok(new { success = false });

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
                    pmt.Status        = "Paid";
                    pmt.PaidAt        = DateTime.UtcNow;
                    pmt.PaymentMethod = "QR";
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, paymentId = pmt.Id });
                }
            }

            return Ok(new { success = false, message = "Không tìm thấy phiếu phù hợp" });
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
