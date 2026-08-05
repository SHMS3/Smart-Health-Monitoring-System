using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Audit;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin;

[Authorize(Roles = "2")]
public class AdminSettingsController : Controller
{
    private readonly IAdminSettingsService _settingsService;
    private readonly IAuditLogService _auditLogService;

    public AdminSettingsController(
        IAdminSettingsService settingsService,
        IAuditLogService auditLogService)
    {
        _settingsService = settingsService;
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

        if (await _settingsService.IsEmailTakenAsync(admin.Id, profile.Email))
        {
            ModelState.AddModelError("Profile.Email", "Email n�y d� du?c s? d?ng b?i t�i kho?n kh�c.");
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

        if (hasChanges)
        {
            await _settingsService.UpdateProfileAsync(admin, profile.FullName, profile.Email);
            await RefreshSignInAsync(admin);

            await _auditLogService.LogAsync(
                "Update",
                "AdminSettings",
                admin.Id.ToString(),
                $"C?p nh?t th�ng tin admin {oldFullName} ({oldEmail}) -> {admin.FullName} ({admin.Email}).",
                admin.Id,
                admin.FullName);

            TempData["Success"] = "�� c?p nh?t th�ng tin qu?n tr? vi�n.";
        }
        else
        {
            TempData["Success"] = "Th�ng tin qu?n tr? vi�n kh�ng c� thay d?i m?i.";
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
            ModelState.AddModelError("Password.CurrentPassword", "T�i kho?n Google kh�ng th? d?i m?t kh?u t?i d�y.");
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        if (!VerifyPassword(password.CurrentPassword, admin.PasswordHash))
        {
            ModelState.AddModelError("Password.CurrentPassword", "M?t kh?u hi?n t?i kh�ng d�ng.");
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        if (VerifyPassword(password.NewPassword, admin.PasswordHash))
        {
            ModelState.AddModelError("Password.NewPassword", "M?t kh?u m?i kh�ng du?c tr�ng v?i m?t kh?u hi?n t?i.");
            return View("Index", CreateSettingsViewModel(admin, "security", password: password));
        }

        await _settingsService.ChangePasswordAsync(admin, password.NewPassword);

        await _auditLogService.LogAsync(
            "ChangePassword",
            "AdminSettings",
            admin.Id.ToString(),
            $"Admin {admin.FullName} d� d?i m?t kh?u t�i kho?n.",
            admin.Id,
            admin.FullName);

        TempData["Success"] = "�� d?i m?t kh?u qu?n tr? vi�n.";
        return RedirectToAction(nameof(Index), new { section = "security" });
    }

    private async Task<User?> GetCurrentAdminAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return await _settingsService.GetCurrentAdminAsync(userId);
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
