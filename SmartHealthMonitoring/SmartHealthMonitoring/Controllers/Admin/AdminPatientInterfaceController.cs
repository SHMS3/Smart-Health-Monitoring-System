using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin;

[Authorize(Roles = "2")]
public class AdminPatientInterfaceController : Controller
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private readonly PatientUiSettingsService _patientUiSettingsService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;

    public AdminPatientInterfaceController(
        PatientUiSettingsService patientUiSettingsService,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment)
    {
        _patientUiSettingsService = patientUiSettingsService;
        _auditLogService = auditLogService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _patientUiSettingsService.GetSettingsAsync(cancellationToken);
        return View("Editor", PatientUiSettingsViewModel.FromSettings(settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PatientUiSettingsViewModel model, CancellationToken cancellationToken)
    {
        model.EnsureOptions();

        if (!PatientUiSettingsViewModel.AllowedLogoIcons.Contains(model.LogoIcon))
        {
            ModelState.AddModelError(nameof(model.LogoIcon), "Biểu tượng logo không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View("Editor", model);
        }

        await TryUploadImageAsync(model.HomeHeroImageFile, nameof(model.HomeHeroImageFile), url => model.HomeHeroImageUrl = url, cancellationToken);
        await TryUploadImageAsync(model.HomeAboutImageFile, nameof(model.HomeAboutImageFile), url => model.HomeAboutImageUrl = url, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View("Editor", model);
        }

        var adminName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Admin";
        await _patientUiSettingsService.UpdateSettingsAsync(model, adminName, cancellationToken);

        await _auditLogService.LogAsync(
            "Update",
            "PatientInterface",
            "patient-ui-settings",
            $"Admin {adminName} cập nhật giao diện bệnh nhân.",
            null,
            adminName);

        TempData["Success"] = "Đã lưu giao diện bệnh nhân.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        var adminName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Admin";
        await _patientUiSettingsService.ResetToDefaultAsync(adminName, cancellationToken);

        await _auditLogService.LogAsync(
            "Reset",
            "PatientInterface",
            "patient-ui-settings",
            $"Admin {adminName} khôi phục giao diện bệnh nhân mặc định.",
            null,
            adminName);

        TempData["Success"] = "Đã khôi phục giao diện bệnh nhân mặc định.";
        return RedirectToAction(nameof(Index));
    }

    private async Task TryUploadImageAsync(
        IFormFile? file,
        string modelStateKey,
        Action<string> setUrl,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return;
        }

        if (file.Length > MaxImageBytes)
        {
            ModelState.AddModelError(modelStateKey, "Ảnh không được vượt quá 5MB.");
            return;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            ModelState.AddModelError(modelStateKey, "Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF.");
            return;
        }

        var uploadDirectory = Path.Combine(_environment.WebRootPath, "images", "patient-ui");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDirectory, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken);

        setUrl($"/images/patient-ui/{fileName}");
    }
}
