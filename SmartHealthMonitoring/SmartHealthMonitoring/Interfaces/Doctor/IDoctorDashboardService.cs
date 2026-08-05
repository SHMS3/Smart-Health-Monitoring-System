using SmartHealthMonitoring.ViewModels.Doctor;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.Common;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHealthMonitoring.Controllers;

namespace SmartHealthMonitoring.Interfaces.Doctor
{
    public interface IDoctorDashboardService
    {
        Task<Models.Doctor?> GetDoctorByUserIdAsync(int userId);
        Task<bool> ToggleShiftAsync(int userId);
        Task<PagedResult<PatientListViewModel>> GetPatientListAsync(int page, int pageSize);
        Task<PatientProfileViewModel?> GetPatientProfileAsync(int patientId, int aiPage, int aiPageSize);
        Task<(PagedResult<WaitingPatient> WaitingPatients, List<int> PatientsWithPayments)> GetWaitingListAsync(int doctorId, int page, int pageSize);
        Task<bool> CancelExamAsync(int waitingPatientId, int doctorId);
        Task<bool> CompleteExamAsync(int patientId, int doctorId);
        Task<(bool Success, string Message, int PatientId)> AcceptPatientAsync(int waitingPatientId, int doctorId);
        Task<List<Service>> GetActiveServicesAsync();
        Task<(bool Success, string Message)> CreatePaymentAsync(CreatePaymentRequest request, int doctorId);
        Task<int> GetUnresolvedAlertCountAsync();
    }
}



