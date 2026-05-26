using Microsoft.AspNetCore.Mvc;

namespace SmartHealthMonitoring.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class DoctorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
