using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Patient;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers;

[Authorize(Roles = "0")]
public class EmergencyContactController : Controller
{
    private readonly IEmergencyContactService _contactService;

    public EmergencyContactController(IEmergencyContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? editId = null)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var patient = await _contactService.GetCurrentPatientAsync(userId);
        
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y h? so b?nh nh�n.";
            return RedirectToAction("Profile", "Home");
        }

        var contacts = await _contactService.GetContactsAsync(patient.Id);
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
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var patient = await _contactService.GetCurrentPatientAsync(userId);
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y h? so b?nh nh�n.";
            return RedirectToAction("Profile", "Home");
        }

        if (!ModelState.IsValid)
        {
            return await InvalidFormViewAsync(patient.Id, form);
        }

        var (isNew, contact, errorEmail, errorPhone) = await _contactService.SaveContactAsync(patient.Id, form);
        bool success = (contact != null);

        if (!success)
        {
            if (!string.IsNullOrEmpty(errorEmail))
                ModelState.AddModelError("Form.Email", errorEmail);
            if (!string.IsNullOrEmpty(errorPhone))
                ModelState.AddModelError("Form.Phone", errorPhone);
                
            return await InvalidFormViewAsync(patient.Id, form);
        }

        TempData["SuccessMessage"] = form.Id.HasValue
            ? "�� c?p nh?t ngu?i li�n h? kh?n c?p."
            : "�� th�m ngu?i li�n h? kh?n c?p.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimary(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var patient = await _contactService.GetCurrentPatientAsync(userId);
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y h? so b?nh nh�n.";
            return RedirectToAction("Profile", "Home");
        }

        var success = await _contactService.SetPrimaryAsync(id, patient.Id);
        if (!success)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y ngu?i li�n h?.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "�� d?t l�m ngu?i li�n h? ch�nh.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var patient = await _contactService.GetCurrentPatientAsync(userId);
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y h? so b?nh nh�n.";
            return RedirectToAction("Profile", "Home");
        }

        var success = await _contactService.ToggleActiveAsync(id, patient.Id);
        bool isActive = success;
        if (!success)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y ngu?i li�n h?.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = isActive
            ? "�� b?t ngu?i li�n h?."
            : "�� t?t ngu?i li�n h?.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var patient = await _contactService.GetCurrentPatientAsync(userId);
        if (patient == null)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y h? so b?nh nh�n.";
            return RedirectToAction("Profile", "Home");
        }

        var success = await _contactService.DeleteAsync(id, patient.Id);
        if (!success)
        {
            TempData["ErrorMessage"] = "Kh�ng t�m th?y ngu?i li�n h?.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "�� x�a ngu?i li�n h? kh?n c?p.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> InvalidFormViewAsync(int patientId, EmergencyContactFormViewModel form)
    {
        return View("Index", new EmergencyContactIndexViewModel
        {
            Contacts = await _contactService.GetContactsAsync(patientId),
            Form = form
        });
    }
}

