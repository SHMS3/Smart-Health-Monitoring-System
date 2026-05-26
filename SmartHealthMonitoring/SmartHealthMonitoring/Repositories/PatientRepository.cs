using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Repositories
{
    public class PatientRepository
    {
        private readonly SmartHealthMonitoringContext _context;

        public PatientRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<Patient?> GetByUserIdAsync(int userId)
        {
            return await _context.Patients
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }
    }
}
