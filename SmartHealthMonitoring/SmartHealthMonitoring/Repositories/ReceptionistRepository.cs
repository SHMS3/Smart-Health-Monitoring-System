using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Repositories
{
    public class ReceptionistRepository
    {
        private readonly SmartHealthMonitoringContext _context;

        public ReceptionistRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<int> GetPendingPaymentsCountAsync()
        {
            return await _context.Payments
                .Where(p => p.Status == "Pending")
                .CountAsync();
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync(int page, int pageSize)
        {
            return await _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Where(p => p.Status == "Pending")
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetPaidPaymentsCountAsync(DateTime start, DateTime end)
        {
            return await _context.Payments
                .Where(p => p.Status == "Paid" && p.CreatedAt >= start && p.CreatedAt <= end)
                .CountAsync();
        }

        public async Task<List<Payment>> GetPaidPaymentsAsync(DateTime start, DateTime end, int page, int pageSize)
        {
            return await _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Where(p => p.Status == "Paid" && p.CreatedAt >= start && p.CreatedAt <= end)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Payment?> GetPaymentDetailsAsync(int id)
        {
            return await _context.Payments
                .Include(p => p.Patient).ThenInclude(pt => pt.User)
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Include(p => p.PaymentDetails).ThenInclude(pd => pd.Service)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Payment?> GetPaymentByIdAsync(int id)
        {
            return await _context.Payments.FindAsync(id);
        }

        public async Task UpdatePaymentAsync(Payment payment)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Payment>> GetPendingPaymentsListAsync()
        {
            return await _context.Payments
                .Where(p => p.Status == "Pending")
                .ToListAsync();
        }

        public IQueryable<Patient> GetPatientsQuery(string? search)
        {
            var query = _context.Patients
                .Include(p => p.User)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(p =>
                    (p.User.FullName != null && p.User.FullName.ToLower().Contains(lowerSearch)) ||
                    (p.Phone != null && p.Phone.Contains(search)) ||
                    (p.User.Email != null && p.User.Email.ToLower().Contains(lowerSearch)) ||
                    (p.CitizenId != null && p.CitizenId.Contains(search))
                );
            }

            return query;
        }
        
        public async Task<int> GetPatientsCountAsync(string? search)
        {
            return await GetPatientsQuery(search).CountAsync();
        }

        public async Task<List<Patient>> GetPatientsAsync(string? search, int page, int pageSize)
        {
            return await GetPatientsQuery(search)
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Patient?> GetPatientByIdAsync(int id)
        {
            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users
                .AnyAsync(u => u.Email == email && !u.IsDeleted);
        }

        public async Task<bool> PhoneExistsAsync(string phone)
        {
            return await _context.Users
                .AnyAsync(u => u.Patients.Any(p => p.Phone == phone) ||
                               u.Doctors.Any(d => d.Phone == phone));
        }

        public async Task<bool> CitizenIdExistsAsync(string citizenId)
        {
            return await _context.Users
                .AnyAsync(u => u.Patients.Any(p => p.CitizenId == citizenId) ||
                               u.Doctors.Any(d => d.CitizenId == citizenId));
        }

        public SmartHealthMonitoringContext GetContext()
        {
             return _context;
        }
        
        public async Task AddUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        
        public async Task AddPatientAsync(Patient patient)
        {
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsPatientInWaitingListAsync(int patientId)
        {
            return await _context.WaitingPatients
                .AnyAsync(w => w.PatientId == patientId && (w.Status == 0 || w.Status == 1));
        }

        public async Task<AppointmentSlot?> GetAvailableSlotAsync(int slotId, int doctorId)
        {
            return await _context.AppointmentSlots
                .FirstOrDefaultAsync(s => s.Id == slotId && s.DoctorId == doctorId && s.Status == AppointmentSlotStatus.Available);
        }
        
        public async Task AddAppointmentAsync(Appointment appointment)
        {
             _context.Appointments.Add(appointment);
             await _context.SaveChangesAsync();
        }

        public async Task<int> GetMaxSequenceNumberTodayAsync(int doctorId, DateTime todayUtc)
        {
            return await _context.WaitingPatients
                .Where(w => w.CreatedAt >= todayUtc && w.DoctorId == doctorId && w.Status != 2)
                .MaxAsync(w => (int?)w.SequenceNumber) ?? 0;
        }

        public async Task AddWaitingPatientAsync(WaitingPatient waitingPatient)
        {
             _context.WaitingPatients.Add(waitingPatient);
             await _context.SaveChangesAsync();
        }

        public async Task<bool> HasSlotsForDateAsync(DateTime todayUtc, DateTime tomorrowUtc)
        {
            return await _context.AppointmentSlots
                .AnyAsync(s => s.SlotStart >= todayUtc && s.SlotStart < tomorrowUtc);
        }

        public async Task<List<dynamic>> GetDoctorsWithSlotsAsync(DateTime todayUtc, DateTime tomorrowUtc)
        {
            return await _context.AppointmentSlots
                .Where(s => s.Status == AppointmentSlotStatus.Available
                         && s.SlotStart >= todayUtc
                         && s.SlotStart < tomorrowUtc)
                .Include(s => s.Doctor).ThenInclude(d => d.User)
                .GroupBy(s => s.DoctorId)
                .Select(g => new
                {
                    doctorId = g.Key,
                    doctorName = g.First().Doctor.User.FullName,
                    specialty = g.First().Doctor.Specialty,
                    roomNumber = g.First().Doctor.RoomNumber,
                    availableSlots = g.Count()
                })
                .OrderBy(d => d.doctorName)
                .ToListAsync<dynamic>();
        }

        public async Task<List<dynamic>> GetDoctorSlotsAsync(int doctorId, DateTime nowUtc, DateTime tomorrowUtc)
        {
            return await _context.AppointmentSlots
                .Where(s => s.DoctorId == doctorId
                         && s.Status == AppointmentSlotStatus.Available
                         && s.SlotStart >= nowUtc
                         && s.SlotStart < tomorrowUtc)
                .OrderBy(s => s.SlotStart)
                .Select(s => new
                {
                    slotId = s.Id,
                    slotStart = s.SlotStart,
                    slotEnd = s.SlotEnd
                })
                .ToListAsync<dynamic>();
        }

        public async Task<List<DoctorWorkSchedule>> GetWorkSchedulesByDayAsync(int dayOfWeek)
        {
            return await _context.DoctorWorkSchedules
                .Where(s => s.IsActive && s.DayOfWeek == dayOfWeek)
                .ToListAsync();
        }

        public async Task<bool> SlotExistsAsync(int doctorId, DateTime slotStartUtc)
        {
            return await _context.AppointmentSlots
                .AnyAsync(s => s.DoctorId == doctorId && s.SlotStart == slotStartUtc);
        }

        public async Task AddAppointmentSlotAsync(AppointmentSlot slot)
        {
             _context.AppointmentSlots.Add(slot);
             await _context.SaveChangesAsync();
        }
        
        public async Task SaveChangesAsync()
        {
             await _context.SaveChangesAsync();
        }
    }
}
