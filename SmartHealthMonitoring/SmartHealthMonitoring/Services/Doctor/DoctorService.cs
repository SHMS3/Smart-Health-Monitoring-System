using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Doctor;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.Doctor
{
    public class DoctorService : IDoctorService
    {
        private readonly SmartHealthMonitoringContext _context;

        public DoctorService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<SmartHealthMonitoring.Models.Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _context.Doctors
                .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted);
        }

        public async Task<List<string>> GetDistinctSpecialtiesAsync()
        {
            return await _context.Doctors.Select(d => d.Specialty).Distinct().ToListAsync();
        }

        public async Task<List<string?>> GetDistinctRoomNumbersAsync()
        {
            return await _context.Doctors.Where(d => d.RoomNumber != null).Select(d => d.RoomNumber).Distinct().ToListAsync();
        }

        public async Task<List<SmartHealthMonitoring.Models.Doctor>> GetAllFilteredDoctorsAsync(string? specialty, string? doctorName, byte? gender, string? roomNumber)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Where(d => !d.IsDeleted);

            if (!string.IsNullOrWhiteSpace(specialty))
                query = query.Where(d => d.Specialty.Contains(specialty));

            if (!string.IsNullOrWhiteSpace(doctorName))
                query = query.Where(d => d.User.FullName.Contains(doctorName));

            if (gender.HasValue)
                query = query.Where(d => d.Sex == gender.Value);

            if (!string.IsNullOrWhiteSpace(roomNumber))
                query = query.Where(d => d.RoomNumber == roomNumber);

            return await query.ToListAsync();
        }
    }
}


