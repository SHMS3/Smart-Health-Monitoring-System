using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers;

[Authorize(Roles = "0")]
public class EmergencyContactController : Controller
{
    private readonly SmartHealthMonitoringContext _context;

    public EmergencyContactController(SmartHealthMonitoringContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? editId = null)
    {
        var patient = await GetCurrentPatientAsync();
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hồ sơ bệnh nhân.";
            return RedirectToAction("Profile", "Home");
        }

        var contacts = await GetContactsAsync(patient.Id);
        var model = new EmergencyContactIndexViewModel
        {
            Contacts = contacts
        };

        if (editId.HasValue)
        {
            var contact = contacts.FirstOrDefault(c => c.Id == editId.Value);
            if (contact != null)
            {
                model.Form = new EmergencyContactFormViewModel
                {
                    Id = contact.Id,
                    FullName = contact.FullName,
                    Relationship = contact.Relationship,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    IsPrimary = contact.IsPrimary,
                    IsActive = contact.IsActive
                };
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] EmergencyContactFormViewModel form)
    {
        var patient = await GetCurrentPatientAsync();
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hồ sơ bệnh nhân.";
            return RedirectToAction("Profile", "Home");
        }

        if (!ModelState.IsValid)
        {
            return await InvalidFormViewAsync(patient.Id, form);
        }

        var normalizedEmail = NormalizeEmail(form.Email);
        var normalizedPhone = NormalizePhone(form.Phone);

        await ValidateUniqueContactMethodAsync(patient.Id, form.Id, normalizedEmail, normalizedPhone);
        if (!ModelState.IsValid)
        {
            return await InvalidFormViewAsync(patient.Id, form);
        }

        EmergencyContact? contact;
        if (form.Id.HasValue)
        {
            contact = await _context.EmergencyContacts
                .FirstOrDefaultAsync(c => c.Id == form.Id.Value && c.PatientId == patient.Id && !c.IsDeleted);

            if (contact == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người liên hệ.";
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            contact = new EmergencyContact
            {
                PatientId = patient.Id,
                CreatedAt = DateTime.Now
            };
            _context.EmergencyContacts.Add(contact);
        }

        contact.FullName = form.FullName.Trim();
        contact.Relationship = form.Relationship.Trim();
        contact.Email = normalizedEmail;
        contact.Phone = normalizedPhone;
        contact.IsPrimary = form.IsPrimary;
        contact.IsActive = form.IsActive;
        contact.UpdatedAt = DateTime.Now;

        if (contact.IsPrimary)
        {
            await ClearOtherPrimaryContactsAsync(patient.Id, contact.Id);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = form.Id.HasValue
            ? "Đã cập nhật người liên hệ khẩn cấp."
            : "Đã thêm người liên hệ khẩn cấp.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimary(int id)
    {
        var contact = await GetOwnedContactAsync(id);
        if (contact == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người liên hệ.";
            return RedirectToAction(nameof(Index));
        }

        await ClearOtherPrimaryContactsAsync(contact.PatientId, contact.Id);
        contact.IsPrimary = true;
        contact.IsActive = true;
        contact.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã đặt làm người liên hệ chính.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var contact = await GetOwnedContactAsync(id);
        if (contact == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người liên hệ.";
            return RedirectToAction(nameof(Index));
        }

        contact.IsActive = !contact.IsActive;
        if (!contact.IsActive)
        {
            contact.IsPrimary = false;
        }

        contact.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = contact.IsActive
            ? "Đã bật nhận SOS cho người liên hệ."
            : "Đã tắt nhận SOS cho người liên hệ.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var contact = await GetOwnedContactAsync(id);
        if (contact == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người liên hệ.";
            return RedirectToAction(nameof(Index));
        }

        contact.IsDeleted = true;
        contact.IsActive = false;
        contact.IsPrimary = false;
        contact.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã xóa người liên hệ khẩn cấp.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<Patient?> GetCurrentPatientAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await _context.Patients
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
    }

    private async Task<List<EmergencyContact>> GetContactsAsync(int patientId)
    {
        return await _context.EmergencyContacts
            .Where(c => c.PatientId == patientId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimary)
            .ThenByDescending(c => c.IsActive)
            .ThenBy(c => c.FullName)
            .ToListAsync();
    }

    private async Task<EmergencyContact?> GetOwnedContactAsync(int id)
    {
        var patient = await GetCurrentPatientAsync();
        if (patient == null)
        {
            return null;
        }

        return await _context.EmergencyContacts
            .FirstOrDefaultAsync(c => c.Id == id && c.PatientId == patient.Id && !c.IsDeleted);
    }

    private async Task ClearOtherPrimaryContactsAsync(int patientId, int currentContactId)
    {
        var contacts = await _context.EmergencyContacts
            .Where(c => c.PatientId == patientId && c.Id != currentContactId && c.IsPrimary && !c.IsDeleted)
            .ToListAsync();

        foreach (var contact in contacts)
        {
            contact.IsPrimary = false;
            contact.UpdatedAt = DateTime.Now;
        }
    }

    private async Task ValidateUniqueContactMethodAsync(
        int patientId,
        int? currentContactId,
        string? email,
        string? phone)
    {
        var contacts = await _context.EmergencyContacts
            .Where(c => c.PatientId == patientId && !c.IsDeleted && (!currentContactId.HasValue || c.Id != currentContactId.Value))
            .Select(c => new { c.Email, c.Phone })
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(email) &&
            contacts.Any(c => string.Equals(NormalizeEmail(c.Email), email, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Form.Email", "Email này đã được khai báo cho người liên hệ khác.");
        }

        if (!string.IsNullOrWhiteSpace(phone) &&
            contacts.Any(c => NormalizePhone(c.Phone) == phone))
        {
            ModelState.AddModelError("Form.Phone", "Số điện thoại này đã được khai báo cho người liên hệ khác.");
        }
    }

    private async Task<IActionResult> InvalidFormViewAsync(int patientId, EmergencyContactFormViewModel form)
    {
        return View("Index", new EmergencyContactIndexViewModel
        {
            Contacts = await GetContactsAsync(patientId),
            Form = form
        });
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeEmail(string? value)
    {
        return NormalizeNullable(value)?.ToLowerInvariant();
    }

    private static string? NormalizePhone(string? value)
    {
        var phone = NormalizeNullable(value);
        if (phone == null)
        {
            return null;
        }

        phone = phone.Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty);

        if (phone.StartsWith("+84", StringComparison.Ordinal))
        {
            return "0" + phone[3..];
        }

        if (phone.StartsWith("84", StringComparison.Ordinal) && phone.Length == 11)
        {
            return "0" + phone[2..];
        }

        return phone;
    }
}
