using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UserController : Controller
    {
        private readonly UserRepository _userRepository;
        public UserController(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        //public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        //{
        //    ViewData["ActivePage"] = "Users";
        //    ViewBag.Status = status;
        //    ViewBag.Search = search;

        //    const int pageSize = 5;

        //    var result = await _userRepository.GetAllUserAsync(search, page, pageSize, status);

        //    return View(result);
        //}

        //public async Task<IActionResult> UpdateAccount(Guid id)
        //{
        //    ViewBag.Roles = await _userRepository.GetAllRoles();

        //    var user = await _userRepository.GetUserVmById(id);

        //    return View(user);
        //}

        //[HttpPost]
        //public async Task<IActionResult> UpdateAccount(Guid id, UserViewModel userVM)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        ViewBag.Roles = await _userRepository.GetAllRoles();
        //        return View(userVM);
        //    }

        //    try
        //    {
        //        _userRepository.UpdateAccount(id, userVM);
        //        TempData["SuccessMessage"] = "Cập nhật tài khoản thành công!";

        //        return RedirectToAction("Index");
        //    }
        //    catch (Exception ex)
        //    {
        //        ModelState.AddModelError("", ex.Message);
        //        ViewBag.Roles = await _userRepository.GetAllRoles();
        //        return View(userVM);
        //    }
        //}

        //[HttpGet]
        //public async Task<IActionResult> LockAccount(Guid id, string status)
        //{
        //    var user = await _userRepository.GetUserVmById(id);

        //    _userRepository.LockAccount(id, status);
        //    TempData["ErrorMessage"] = $"Tài khoản {user.Email} đã bị khoá!";

        //    return RedirectToAction("Index");
        //}
    }
}
