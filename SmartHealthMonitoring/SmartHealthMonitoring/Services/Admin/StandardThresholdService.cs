using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services.Admin
{
    public class StandardThresholdService : IStandardThresholdService
    {
        private readonly SmartHealthMonitoringContext _context;

        public StandardThresholdService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<List<StandardThreshold>> GetAllAsync()
        {
            return await _context.StandardThresholds
                .OrderBy(t => t.Sex)
                .ThenBy(t => t.AgeMin)
                .ToListAsync();
        }

        public async Task<StandardThreshold?> GetByIdAsync(int id)
        {
            return await _context.StandardThresholds.FindAsync(id);
        }

        public async Task<StandardThreshold> CreateAsync(StandardThresholdViewModel model)
        {
            var entity = new StandardThreshold();
            MapToEntity(model, entity);
            entity.CreatedAt = SmartHealthMonitoring.Common.AppTime.Now;
            entity.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

            _context.StandardThresholds.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<StandardThreshold?> UpdateAsync(int id, StandardThresholdViewModel model)
        {
            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null) return null;

            MapToEntity(model, entity);
            entity.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<StandardThreshold?> ToggleActiveAsync(int id)
        {
            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null) return null;

            entity.IsActive = !entity.IsActive;
            entity.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null) return false;

            _context.StandardThresholds.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        private static void MapToEntity(StandardThresholdViewModel vm, StandardThreshold entity)
        {
            entity.Name = vm.Name;
            entity.Description = vm.Description;
            entity.Sex = vm.Sex;
            entity.AgeMin = vm.AgeMin;
            entity.AgeMax = vm.AgeMax;
            entity.SystolicBpWarning = vm.SystolicBpWarning;
            entity.SystolicBpDanger = vm.SystolicBpDanger;
            entity.DiastolicBpWarning = vm.DiastolicBpWarning;
            entity.DiastolicBpDanger = vm.DiastolicBpDanger;
            entity.HeartRateWarningMin = vm.HeartRateWarningMin;
            entity.HeartRateDangerMin = vm.HeartRateDangerMin;
            entity.HeartRateWarningMax = vm.HeartRateWarningMax;
            entity.HeartRateDangerMax = vm.HeartRateDangerMax;
            entity.IsActive = vm.IsActive;
        }
    }
}
