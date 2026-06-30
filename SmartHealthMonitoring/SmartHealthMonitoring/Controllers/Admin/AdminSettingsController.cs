using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin;

[Authorize(Roles = "2")]
public class AdminSettingsController : Controller
{
    private readonly SmartHealthMonitoringContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminSettingsController(
        SmartHealthMonitoringContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string section = "profile")
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null)
        {
            return RedirectToAction("Login", "Auth");
        }

        return View(CreateSettingsViewModel(admin, section));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] AdminProfileSettingsViewModel profile)
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null)
        {
            return RedirectToAction("Login", "Auth");
        }

        profile.FullName = (profile.FullName ?? string.Empty).Trim();
        profile.Email = (profile.Email ?? string.Empty).Trim();

        if (await _context.Users.AnyAsync(u => u.Id != admin.Id && u.Email == profile.Email))
        {
            ModelState.AddModelError("Profile.Email", "Email này đã được sử dụng bởi tài khoản khác.");
        }

        if (!ModelState.IsValid)
        {
            profile.UserId = admin.Id;
            profile.CreatedAt = admin.CreatedAt;
            return View("Index", CreateSettingsViewModel(admin, "profile", profile));
        }

        var oldFullName = admin.FullName;
        var oldEmail = admin.Email;
        var hasChanges = oldFullName != profile.FullName || oldEmail != profile.Email;

        admin.FullName = profile.FullName;
        admin.Email = profile.Email;

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
            await RefreshSignInAsync(admin);

            await _auditLogService.LogAsync(
                "Update",
                "AdminSettings",
                admin.Id.ToString(),
                $"Cập nhật thông tin admin {oldFullName} ({oldEmail}) -> {admin.FullName} ({admin.Email}).",
                admin.Id,
                admin.FullName);

            TempData["Success"] = "Đã cập nhật thông tin quản trị viên.";
        }
        else
        {
            TempData["Success"] = "Thông tin quản trị viên không có thay đổi mới.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel password)
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        if (string.IsNullOrWhiteSpace(admin.PasswordHash))
        {
            ModelState.AddModelError("Password.CurrentPassword", "Tài khoản Google không thể đổi mật khẩu tại đây.");
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        if (!VerifyPassword(password.CurrentPassword, admin.PasswordHash))
        {
            ModelState.AddModelError("Password.CurrentPassword", "Mật khẩu hiện tại không đúng.");
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        if (VerifyPassword(password.NewPassword, admin.PasswordHash))
        {
            ModelState.AddModelError("Password.NewPassword", "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password.NewPassword);
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "ChangePassword",
            "AdminSettings",
            admin.Id.ToString(),
            $"Admin {admin.FullName} đã đổi mật khẩu tài khoản.",
            admin.Id,
            admin.FullName);

        TempData["Success"] = "Đã đổi mật khẩu quản trị viên.";
        return RedirectToAction(nameof(Index), new { section = "security" });
    }

    private async Task<User?> GetCurrentAdminAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.Role == 2 && !u.IsDeleted);
    }

    private static AdminSettingsViewModel CreateSettingsViewModel(
        User admin,
        string activeSection = "profile",
        AdminProfileSettingsViewModel? profile = null,
        ChangePasswordViewModel? password = null)
    {
        return new AdminSettingsViewModel
        {
            ActiveSection = activeSection,
            IsGoogleAccount = string.IsNullOrWhiteSpace(admin.PasswordHash),
            Profile = profile ?? new AdminProfileSettingsViewModel
            {
                UserId = admin.Id,
                FullName = admin.FullName,
                Email = admin.Email,
                CreatedAt = admin.CreatedAt
            },
            Password = password ?? new ChangePasswordViewModel()
        };
    }

    private async Task RefreshSignInAsync(User admin)
    {
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = authResult.Properties?.IsPersistent ?? false,
            ExpiresUtc = authResult.Properties?.ExpiresUtc
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.Email),
            new(ClaimTypes.Email, admin.Email),
            new("FullName", admin.FullName),
            new(ClaimTypes.Role, admin.Role.ToString())
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            authProperties);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        if (IsBcryptHash(passwordHash))
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }

        return password == passwordHash;
    }

    private static bool IsBcryptHash(string passwordHash)
    {
        return passwordHash.StartsWith("$2a$")
            || passwordHash.StartsWith("$2b$")
            || passwordHash.StartsWith("$2y$");
    }
}
