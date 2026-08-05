using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Patient;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services.Patient
{
    public class EmergencyContactService : IEmergencyContactService
    {
        private readonly SmartHealthMonitoringContext _context;

        public EmergencyContactService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<SmartHealthMonitoring.Models.Patient?> GetCurrentPatientAsync(int userId)
        {
            return await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
        }

        public async Task<List<EmergencyContact>> GetContactsAsync(int patientId)
        {
            return await _context.EmergencyContacts
                .Where(c => c.PatientId == patientId && !c.IsDeleted)
                .OrderByDescending(c => c.IsPrimary)
                .ThenByDescending(c => c.IsActive)
                .ThenBy(c => c.FullName)
                .ToListAsync();
        }

        public async Task<(bool isNew, EmergencyContact? contact, string? emailError, string? phoneError)> SaveContactAsync(int patientId, EmergencyContactFormViewModel form)
        {
            var normalizedEmail = NormalizeEmail(form.Email);
            var normalizedPhone = NormalizePhone(form.Phone);

            var (emailError, phoneError) = await ValidateUniqueContactMethodAsync(patientId, form.Id, normalizedEmail, normalizedPhone);
            if (emailError != null || phoneError != null)
            {
                return (false, null, emailError, phoneError);
            }

            EmergencyContact? contact;
            bool isNew = false;
            
            if (form.Id.HasValue)
            {
                contact = await _context.EmergencyContacts
                    .FirstOrDefaultAsync(c => c.Id == form.Id.Value && c.PatientId == patientId && !c.IsDeleted);

                if (contact == null)
                {
                    return (false, null, null, null);
                }
            }
            else
            {
                isNew = true;
                contact = new EmergencyContact
                {
                    PatientId = patientId,
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
                await ClearOtherPrimaryContactsAsync(patientId, contact.Id);
            }

            await _context.SaveChangesAsync();

            return (isNew, contact, null, null);
        }

        public async Task<EmergencyContact?> GetOwnedContactAsync(int contactId, int patientId)
        {
            return await _context.EmergencyContacts
                .FirstOrDefaultAsync(c => c.Id == contactId && c.PatientId == patientId && !c.IsDeleted);
        }

        public async Task<bool> SetPrimaryAsync(int contactId, int patientId)
        {
            var contact = await GetOwnedContactAsync(contactId, patientId);
            if (contact == null)
            {
                return false;
            }

            await ClearOtherPrimaryContactsAsync(contact.PatientId, contact.Id);
            contact.IsPrimary = true;
            contact.IsActive = true;
            contact.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleActiveAsync(int contactId, int patientId)
        {
            var contact = await GetOwnedContactAsync(contactId, patientId);
            if (contact == null)
            {
                return false;
            }

            contact.IsActive = !contact.IsActive;
            if (!contact.IsActive)
            {
                contact.IsPrimary = false;
            }

            contact.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int contactId, int patientId)
        {
            var contact = await GetOwnedContactAsync(contactId, patientId);
            if (contact == null)
            {
                return false;
            }

            contact.IsDeleted = true;
            contact.IsActive = false;
            contact.IsPrimary = false;
            contact.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
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

        private async Task<(string? emailError, string? phoneError)> ValidateUniqueContactMethodAsync(
            int patientId,
            int? currentContactId,
            string? email,
            string? phone)
        {
            var contacts = await _context.EmergencyContacts
                .Where(c => c.PatientId == patientId && !c.IsDeleted && (!currentContactId.HasValue || c.Id != currentContactId.Value))
                .Select(c => new { c.Email, c.Phone })
                .ToListAsync();

            string? emailError = null;
            string? phoneError = null;

            if (!string.IsNullOrWhiteSpace(email) &&
                contacts.Any(c => string.Equals(NormalizeEmail(c.Email), email, StringComparison.OrdinalIgnoreCase)))
            {
                emailError = "Email n�y d� du?c khai b�o cho ngu?i li�n h? kh�c.";
            }

            if (!string.IsNullOrWhiteSpace(phone) &&
                contacts.Any(c => NormalizePhone(c.Phone) == phone))
            {
                phoneError = "S? di?n tho?i n�y d� du?c khai b�o cho ngu?i li�n h? kh�c.";
            }

            return (emailError, phoneError);
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
}

