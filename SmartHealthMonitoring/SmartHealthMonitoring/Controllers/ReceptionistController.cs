using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.Interfaces;
using System;
using System.Threading.Tasks;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "2,3")] 
    public class ReceptionistController : Controller
    {
        private const string BANK_ID = "MB";          // Mã ngân hàng (MBBank)
        private const string ACCOUNT_NO = "1508200456788";  // Số tài khoản
        private const string ACCOUNT_NAME = "PHAM THE SON"; // Tên chủ TK
        
        private readonly IReceptionistService _receptionistService;
        private readonly IAppointmentService _appointmentService;
        private readonly IEmailTriggerService _emailTriggerService;

        public ReceptionistController(
            IReceptionistService receptionistService,
            IAppointmentService appointmentService,
            IEmailTriggerService emailTriggerService)
        {
            _receptionistService = receptionistService;
            _appointmentService = appointmentService;
            _emailTriggerService = emailTriggerService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var result = await _receptionistService.GetPendingPaymentsAsync(page, pageSize);
            return View(result);
        }

        public async Task<IActionResult> PaymentHistory(DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10)
        {
            if (!fromDate.HasValue) fromDate = DateTime.Today;
            if (!toDate.HasValue) toDate = DateTime.Today;

            var result = await _receptionistService.GetPaidPaymentsAsync(fromDate.Value, toDate.Value, page, pageSize);

            ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

            return View(result);
        }

        public async Task<IActionResult> Details(int id)
        {
            var payment = await _receptionistService.GetPaymentDetailsAsync(id);
            if (payment == null) return NotFound();
            return View(payment);
        }

        public async Task<IActionResult> Checkout(int id)
        {
            var payment = await _receptionistService.GetPaymentDetailsAsync(id);

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
            var (success, message) = await _receptionistService.ConfirmCashAsync(id);
            return Json(new { success, message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            return await ConfirmCash(id);
        }

        [HttpGet]
        public async Task<IActionResult> CheckQrPayment(int id)
        {
            var (paid, message) = await _receptionistService.CheckQrPaymentStatusAsync(id);
            return Json(new { paid, message });
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SepayWebhook([FromBody] SepayWebhookPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Content))
            {
                Console.WriteLine("SepayWebhook: Payload is null or Content is empty.");
                return Ok(new { success = false, message = "Empty payload" });
            }

            Console.WriteLine($"SepayWebhook RECEIVED: Amount={payload.TransferAmount}, Content={payload.Content}");

            var (success, message, paymentId) = await _receptionistService.ProcessSepayWebhookAsync(payload.Content, payload.TransferAmount ?? 0);
            
            if (success)
                return Ok(new { success = true, paymentId });
            else
                return Ok(new { success = false, message });
        }

        public async Task<IActionResult> Patients(string search, int page = 1, int pageSize = 10)
        {
            var result = await _receptionistService.GetPatientsAsync(search, page, pageSize);

            var viewModel = new ReceptionistPatientListViewModel
            {
                Patients = result,
                SearchQuery = search
            };

            return View(viewModel);
        }

        public async Task<IActionResult> PatientDetails(int id)
        {
            var patient = await _receptionistService.GetPatientDetailsAsync(id);
            if (patient == null) return NotFound();

            return View(patient);
        }

        public IActionResult RegisterPatient()
        {
            return View(new ReceptionistRegisterPatientViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPatient(ReceptionistRegisterPatientViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, message) = await _receptionistService.RegisterPatientAsync(model);
            
            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction(nameof(Patients));
            }
            else
            {
                if (message.Contains("Email")) ModelState.AddModelError("Email", message);
                else if (message.Contains("điện thoại")) ModelState.AddModelError("Phone", message);
                else if (message.Contains("CCCD")) ModelState.AddModelError("CitizenId", message);
                else if (message.Contains("Ngày sinh")) ModelState.AddModelError("DateOfBirth", message);
                else ModelState.AddModelError(string.Empty, message);
                
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

            var (success, message) = await _receptionistService.AddToWaitingListAsync(patientId, doctorId, slotId, receptionistId);

            if (success) TempData["Success"] = message;
            else TempData["Error"] = message;

            return RedirectToAction(nameof(Patients));
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableDoctors()
        {
            var doctors = await _receptionistService.GetAvailableDoctorsAsync();
            return Json(new { success = true, data = doctors });
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctorSlots(int doctorId)
        {
            var slots = await _receptionistService.GetDoctorSlotsAsync(doctorId);
            return Json(new { success = true, data = slots });
        }

        [HttpGet]
        public async Task<IActionResult> PendingAppointments()
        {
            var pendingList = await _appointmentService.GetPendingAppointmentsAsync();
            return View(pendingList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int appointmentId)
        {
            var success = await _appointmentService.ApproveAppointmentBookingAsync(appointmentId);
            if (success)
            {
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

    public class SepayWebhookPayload
    {
        public string? Content { get; set; }
        public decimal? TransferAmount { get; set; }
        public string? ReferenceCode { get; set; }
    }
}
