using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;

        public HomeController(ILogger<HomeController> logger, SmartHealthMonitoringContext context, IEmailService emailService)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Contact()
        {
            var numberOfDoctor = _context.Doctors.Count();
            var numberOfPatient = _context.Patients.Count();

            ViewBag.NumberOfDoctor = numberOfDoctor;
            ViewBag.NumberOfPatient = numberOfPatient;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(
    ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string html = $@"
        <h2>Liên hệ mới từ SmartHealth</h2>

        <p>
            <b>Họ tên:</b>
            {model.FullName}
        </p>

        <p>
            <b>Email:</b>
            {model.Email}
        </p>

        <p>
            <b>Điện thoại:</b>
            {model.Phone}
        </p>

        <p>
            <b>Nội dung:</b>
        </p>

        <div>
            {model.Message}
        </div>";

            await _emailService.SendEmailAsync("namntp27@gmail.com","Liên hệ mới từ website", html);

            TempData["Success"] = "Gửi thành công. Chúng tôi sẽ phản hồi trong vòng 24 giờ.";


            return RedirectToAction(nameof(Contact));
        }

        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public IActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}
    }
}
