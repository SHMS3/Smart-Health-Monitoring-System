using Microsoft.AspNetCore.Mvc;

namespace SmartHealthMonitoring.Controllers
{
    public class ChatbotController : Controller
    {
        public async Task<IActionResult> Index()
        {
            return View();
        }

        public async Task<IActionResult> History()
        {
            return View();
        }
    }
}
