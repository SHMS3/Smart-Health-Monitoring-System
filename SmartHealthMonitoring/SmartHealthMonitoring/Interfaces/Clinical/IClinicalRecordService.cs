using SmartHealthMonitoring.ViewModels;
using System;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces.Clinical
{
    public interface IClinicalRecordService
    {
        Task<int?> GetPatientIdByEmailAsync(string email);
        
        Task<(bool success, string message, PatientRecordIndexViewModel? viewModel, int? redirectPatientId)> GetPatientRecordIndexViewModelAsync(
            int id, 
            string currentEmail, 
            bool isPatientRole, 
            bool isDoctorRole,
            int page, 
            int pageSize, 
            int diaryPage, 
            int diaryPageSize, 
            DateTime? searchDate, 
            string activeTab);

        Task<(bool success, string message, int? redirectPatientId)> DeleteClinicalRecordAsync(int recordId);
        Task<(bool success, string message, int? redirectPatientId)> ToggleViewForPatientAsync(int recordId);
    }
}
