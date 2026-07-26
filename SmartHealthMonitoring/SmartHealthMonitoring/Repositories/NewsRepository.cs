using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Repositories
{
    public class NewsRepository
    {
        private readonly SmartHealthMonitoringContext _context;

        public NewsRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<List<HealthNewsPost>> GetNewsAsync(string? status)
        {
            var query = _context.HealthNewsPosts.AsQueryable();
            
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(n => n.Status == status);
            }

            return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
        }

        public async Task<HealthNewsPost?> GetNewsByIdAsync(int id)
        {
            return await _context.HealthNewsPosts.FindAsync(id);
        }

        public async Task AddNewsAsync(HealthNewsPost post)
        {
            _context.HealthNewsPosts.Add(post);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateNewsAsync(HealthNewsPost post)
        {
            _context.HealthNewsPosts.Update(post);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteNewsAsync(HealthNewsPost post)
        {
            _context.HealthNewsPosts.Remove(post);
            await _context.SaveChangesAsync();
        }
    }
}
