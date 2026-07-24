using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Repositories
{
    public class ClinicalRecordRepository
    {
        private readonly SmartHealthMonitoringContext _context;

        public ClinicalRecordRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<Patient?> GetPatientByEmailAsync(string email)
        {
            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.User.Email == email && !p.IsDeleted);
        }

        public async Task<Patient?> GetPatientByIdAsync(int id)
        {
            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public IQueryable<ClinicalRecord> GetClinicalRecordsQuery(int patientId, bool isPatientRole)
        {
            var query = _context.ClinicalRecords
                .Where(r => r.PatientId == patientId && !r.IsDeleted);

            if (isPatientRole)
            {
                query = query.Where(r => r.IsViewForPatient);
            }

            return query.OrderByDescending(r => r.VisitDate);
        }

        public IQueryable<DailyVitalLog> GetDailyVitalLogsQuery(int patientId, DateTime? searchDate)
        {
            var query = _context.DailyVitalLogs
                .Where(d => d.PatientId == patientId && !d.IsDeleted);

            if (searchDate.HasValue)
            {
                var dateStart = searchDate.Value.Date;
                var dateEnd = searchDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(d => d.LoggedAt >= dateStart && d.LoggedAt <= dateEnd);
            }

            return query.OrderByDescending(d => d.LoggedAt);
        }

        public async Task<int> GetTodayPaidPaymentsCountAsync(int patientId, DateTime todayDate)
        {
            return await _context.Payments
                .CountAsync(p => p.PatientId == patientId && p.Status == "Paid" && p.CreatedAt.Date == todayDate.Date);
        }

        public async Task<int> GetTodayClinicalRecordsCountAsync(int patientId, DateTime todayDate)
        {
            return await _context.ClinicalRecords
                .CountAsync(r => r.PatientId == patientId && r.VisitDate.Date == todayDate.Date && !r.IsDeleted);
        }

        public async Task<bool> HasConfiguredThresholdsAsync(int patientId)
        {
            return await _context.PatientThresholds.AnyAsync(t => t.PatientId == patientId);
        }

        public async Task<ClinicalRecord?> GetClinicalRecordByIdAsync(int recordId)
        {
            return await _context.ClinicalRecords
                .Include(r => r.Patient)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == recordId);
        }

        public async Task UpdateClinicalRecordAsync(ClinicalRecord record)
        {
            _context.ClinicalRecords.Update(record);
            await _context.SaveChangesAsync();
        }
    }
}
