using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces.Clinical
{
    public interface IClinicalExamService
    {
        Task<SmartHealthMonitoring.Models.Doctor?> GetDoctorByUserIdAsync(int userId);
        Task<(Payment? payment, List<string> purchasedServiceNames)> GetAvailablePaymentAsync(int patientId, int doctorId);
        Task<ClinicalRecord?> CreateClinicalExamAsync(ClinicalExamFormViewModel model, int doctorId);
        Task<StandardThreshold?> GetSuggestedThresholdAsync(int patientId, int doctorId);
        Task<List<StandardThreshold>> GetAllStandardThresholdsAsync();
        Task<PatientThreshold?> GetPatientThresholdAsync(int patientId);
        Task<bool> SavePatientThresholdAsync(int patientId, int doctorId, PatientThresholdViewModel model);
    }
}

