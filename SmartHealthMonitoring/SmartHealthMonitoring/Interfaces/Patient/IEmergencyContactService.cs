using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Interfaces.Patient
{
    public interface IEmergencyContactService
    {
        Task<SmartHealthMonitoring.Models.Patient?> GetCurrentPatientAsync(int userId);
        Task<List<EmergencyContact>> GetContactsAsync(int patientId);
        Task<(bool isNew, EmergencyContact? contact, string? emailError, string? phoneError)> SaveContactAsync(int patientId, EmergencyContactFormViewModel form);
        Task<EmergencyContact?> GetOwnedContactAsync(int contactId, int patientId);
        Task<bool> SetPrimaryAsync(int contactId, int patientId);
        Task<bool> ToggleActiveAsync(int contactId, int patientId);
        Task<bool> DeleteAsync(int contactId, int patientId);
    }
}

