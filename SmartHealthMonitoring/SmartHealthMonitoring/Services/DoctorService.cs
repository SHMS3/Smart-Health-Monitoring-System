using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly SmartHealthMonitoringContext _context;

        public DoctorService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }
        public async Task<Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _context.Doctors
                .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted);
        }
    }
}
