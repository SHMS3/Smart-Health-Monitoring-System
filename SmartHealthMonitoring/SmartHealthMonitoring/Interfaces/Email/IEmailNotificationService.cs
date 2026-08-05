using System;
using System.Threading.Tasks;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Interfaces.Email
{
    public interface IEmailNotificationService
    {
        Task<EmailHistoryIndexViewModel> GetFilteredAsync(
            int? currentDoctorId,
            bool isDoctorRole,
            byte? status,
            string? emailType,
            DateTime? fromDate,
            DateTime? toDate,
            string? keyword,
            int? patientId,
            string? sender,
            int page,
            int pageSize);
    }
}
