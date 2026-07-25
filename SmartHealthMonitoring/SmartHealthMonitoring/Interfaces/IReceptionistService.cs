using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces
{
    public interface IReceptionistService
    {
        Task<PagedResult<Payment>> GetPendingPaymentsAsync(int page, int pageSize);
        Task<PagedResult<Payment>> GetPaidPaymentsAsync(DateTime fromDate, DateTime toDate, int page, int pageSize);
        Task<Payment?> GetPaymentDetailsAsync(int id);
        Task<Payment?> GetPaymentByIdAsync(int id);
        Task<(bool success, string message)> ConfirmCashAsync(int id);
        Task<(bool paid, string message)> CheckQrPaymentStatusAsync(int id);
        Task<(bool success, string message, int? paymentId)> ProcessSepayWebhookAsync(string content, decimal amount);
        Task<PagedResult<Patient>> GetPatientsAsync(string search, int page, int pageSize);
        Task<Patient?> GetPatientDetailsAsync(int id);
        Task<(bool success, string message)> RegisterPatientAsync(ReceptionistRegisterPatientViewModel model);
        Task<(bool success, string message)> AddToWaitingListAsync(int patientId, int doctorId, int slotId, int receptionistId);
        Task<List<dynamic>> GetAvailableDoctorsAsync();
        Task<List<dynamic>> GetDoctorSlotsAsync(int doctorId);
    }
}
