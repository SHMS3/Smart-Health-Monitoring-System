using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Patient;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services.Patient
{
    public class ProfileService : IProfileService
    {
        private readonly SmartHealthMonitoringContext _context;

        public ProfileService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        }

        public async Task<SmartHealthMonitoring.Models.Patient?> GetPatientByUserIdAsync(int userId)
        {
            return await _context.Patients
                .Include(p => p.User)
                .Include(p => p.PatientHabit)
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
        }

        public async Task<SmartHealthMonitoring.Models.Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
        }

        public async Task UpdateProfileAsync(int userId, UpdateProfileViewModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null) return;

            if (user.Role == 0 || user.Role == 1)
            {
                user.FullName = model.FullName;
                _context.Users.Update(user);
            }

            if (user.Role == 0)
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
                if (patient != null)
                {
                    if (patient.Phone != model.Phone)
                        patient.IsPhoneVerified = false;

                    if (model.DateOfBirth.HasValue)
                        patient.DateOfBirth = model.DateOfBirth.Value;
                    
                    if (model.Sex.HasValue)
                        patient.Sex = model.Sex.Value;

                    patient.Phone = model.Phone;
                    patient.Address = model.Address;
                    patient.CitizenId = model.CitizenId;

                    _context.Patients.Update(patient);
                }
            }
            else if (user.Role == 1)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor != null)
                {
                    if (doctor.Phone != model.Phone)
                        doctor.IsPhoneVerified = false;

                    if (model.DateOfBirth.HasValue)
                        doctor.DateOfBirth = model.DateOfBirth.Value;
                        
                    if (model.Sex.HasValue)
                        doctor.Sex = model.Sex.Value;

                    doctor.Phone = model.Phone;
                    doctor.Address = model.Address;
                    doctor.CitizenId = model.CitizenId;

                    _context.Doctors.Update(doctor);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateHabitsAsync(int userId, HabitViewModel model)
        {
            var patient = await _context.Patients
                .Include(p => p.PatientHabit)
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

            if (patient == null) return;

            if (patient.PatientHabit == null)
            {
                var habit = new PatientHabit
                {
                    PatientId = patient.Id,
                    DietSalty = model.DietSalty,
                    DietHighFat = model.DietHighFat,
                    DietHighSugar = model.DietHighSugar,
                    DietLowFiber = model.DietLowFiber,
                    AlcoholHeavy = model.AlcoholHeavy,
                    CaffeineSpike = model.CaffeineSpike,
                    LifestyleSedentary = model.LifestyleSedentary,
                    LifestyleSitLong = model.LifestyleSitLong,
                    SleepDeprived = model.SleepDeprived,
                    NoHealthCheck = model.NoHealthCheck,
                    SmokeActive = model.SmokeActive,
                    SmokePassive = model.SmokePassive,
                    SelfMedication = model.SelfMedication,
                    StressHigh = model.StressHigh,
                    ExerciseRegularly = model.ExerciseRegularly,
                    SleepEarly = model.SleepEarly,
                    DrinkEnoughWater = model.DrinkEnoughWater,
                    DietBalanced = model.DietBalanced,
                    RegularHealthCheck = model.RegularHealthCheck,
                    NoSubstanceAbuse = model.NoSubstanceAbuse,
                    UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now
                };
                _context.PatientHabits.Add(habit);
            }
            else
            {
                var h = patient.PatientHabit;
                h.DietSalty = model.DietSalty;
                h.DietHighFat = model.DietHighFat;
                h.DietHighSugar = model.DietHighSugar;
                h.DietLowFiber = model.DietLowFiber;
                h.AlcoholHeavy = model.AlcoholHeavy;
                h.CaffeineSpike = model.CaffeineSpike;
                h.LifestyleSedentary = model.LifestyleSedentary;
                h.LifestyleSitLong = model.LifestyleSitLong;
                h.SleepDeprived = model.SleepDeprived;
                h.NoHealthCheck = model.NoHealthCheck;
                h.SmokeActive = model.SmokeActive;
                h.SmokePassive = model.SmokePassive;
                h.SelfMedication = model.SelfMedication;
                h.StressHigh = model.StressHigh;
                h.ExerciseRegularly = model.ExerciseRegularly;
                h.SleepEarly = model.SleepEarly;
                h.DrinkEnoughWater = model.DrinkEnoughWater;
                h.DietBalanced = model.DietBalanced;
                h.RegularHealthCheck = model.RegularHealthCheck;
                h.NoSubstanceAbuse = model.NoSubstanceAbuse;
                h.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;
                _context.PatientHabits.Update(h);
            }

            await _context.SaveChangesAsync();
        }

        public async Task ChangePasswordAsync(int userId, string newPasswordHash)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user != null)
            {
                user.PasswordHash = newPasswordHash;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdatePhoneAsync(int userId, string phone)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null) return;

            if (user.Role == 0)
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
                if (patient != null)
                {
                    patient.Phone = phone;
                    patient.IsPhoneVerified = true;
                    _context.Patients.Update(patient);
                }
            }
            else if (user.Role == 1)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor != null)
                {
                    doctor.Phone = phone;
                    doctor.IsPhoneVerified = true;
                    _context.Doctors.Update(doctor);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateAvatarAsync(int userId, string avatarUrl)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.AvatarUrl = avatarUrl;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<string?> GetAvatarUrlAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            return user?.AvatarUrl;
        }

        public async Task<(int DailyLogCount, int ClinicalRecordCount, int AlertCount, DateTime? LastLogDate)> GetProfileStatsAsync(int userId)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
            if (patient == null) return (0, 0, 0, null);

            var dailyLogCount = await _context.DailyVitalLogs.CountAsync(v => v.PatientId == patient.Id);
            var clinicalRecordCount = await _context.ClinicalRecords.CountAsync(c => c.PatientId == patient.Id);
            var alertCount = await _context.WarningAlerts.CountAsync(w => w.PatientId == patient.Id && !w.IsDeleted);
            var lastLogDate = await _context.DailyVitalLogs
                .Where(v => v.PatientId == patient.Id)
                .OrderByDescending(v => v.LoggedAt)
                .Select(v => (DateTime?)v.LoggedAt)
                .FirstOrDefaultAsync();

            return (dailyLogCount, clinicalRecordCount, alertCount, lastLogDate);
        }

        public async Task<List<HealthNewsPost>> GetPublishedNewsAsync(int take = 9)
        {
            return await _context.HealthNewsPosts
                .Where(n => n.Status == "Published")
                .OrderByDescending(n => n.PublishedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<(List<HealthNewsPost> Items, int TotalCount)> GetNewsPagedAsync(string? keyword, int page, int pageSize)
        {
            var query = _context.HealthNewsPosts.Where(n => n.Status == "Published");

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(n => n.Title.Contains(keyword) || n.Summary.Contains(keyword));
            }

            int totalCount = await query.CountAsync();
            
            var items = await query
                .OrderByDescending(n => n.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(HealthNewsPost? Post, List<HealthNewsPost> Related)> GetNewsDetailAsync(int id)
        {
            var post = await _context.HealthNewsPosts
                .FirstOrDefaultAsync(n => n.Id == id && n.Status == "Published");

            var related = await _context.HealthNewsPosts
                .Where(n => n.Status == "Published" && n.Id != id)
                .OrderByDescending(n => n.PublishedAt)
                .Take(5)
                .ToListAsync();

            return (post, related);
        }

        public async Task<int> GetDoctorCountAsync()
        {
            return await _context.Doctors.CountAsync();
        }

        public async Task<int> GetPatientCountAsync()
        {
            return await _context.Patients.CountAsync();
        }

        public async Task<HabitViewModel?> GetHabitsAsync(int userId)
        {
            var patient = await _context.Patients
                .Include(p => p.PatientHabit)
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

            if (patient?.PatientHabit == null) return null;

            var h = patient.PatientHabit;
            return new HabitViewModel
            {
                DietSalty = h.DietSalty,
                DietHighFat = h.DietHighFat,
                DietHighSugar = h.DietHighSugar,
                DietLowFiber = h.DietLowFiber,
                AlcoholHeavy = h.AlcoholHeavy,
                CaffeineSpike = h.CaffeineSpike,
                LifestyleSedentary = h.LifestyleSedentary,
                LifestyleSitLong = h.LifestyleSitLong,
                SleepDeprived = h.SleepDeprived,
                NoHealthCheck = h.NoHealthCheck,
                SmokeActive = h.SmokeActive,
                SmokePassive = h.SmokePassive,
                SelfMedication = h.SelfMedication,
                StressHigh = h.StressHigh,
                ExerciseRegularly = h.ExerciseRegularly,
                SleepEarly = h.SleepEarly,
                DrinkEnoughWater = h.DrinkEnoughWater,
                DietBalanced = h.DietBalanced,
                RegularHealthCheck = h.RegularHealthCheck,
                NoSubstanceAbuse = h.NoSubstanceAbuse
            };
        }
    }
}


