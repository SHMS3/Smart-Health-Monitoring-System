using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Repositories;

public interface IClinicalRecordRepository
{
    Task<SmartHealthMonitoring.Models.Patient?> GetPatientByEmailAsync(string email);
    Task<SmartHealthMonitoring.Models.Patient?> GetPatientByIdAsync(int id);
    IQueryable<ClinicalRecord> GetClinicalRecordsQuery(int patientId, bool isPatientRole);
    IQueryable<DailyVitalLog> GetDailyVitalLogsQuery(int patientId, DateTime? searchDate);
    Task<int> GetTodayPaidPaymentsCountAsync(int patientId, DateTime todayDate);
    Task<int> GetTodayClinicalRecordsCountAsync(int patientId, DateTime todayDate);
    Task<bool> HasConfiguredThresholdsAsync(int patientId);
    Task<ClinicalRecord?> GetClinicalRecordByIdAsync(int recordId);
    Task UpdateClinicalRecordAsync(ClinicalRecord record);
}
