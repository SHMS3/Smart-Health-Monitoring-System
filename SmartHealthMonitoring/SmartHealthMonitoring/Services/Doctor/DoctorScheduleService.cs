using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Appointment;
using SmartHealthMonitoring.Interfaces.Doctor;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Doctor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services.Doctor
{
    public class DoctorScheduleService : IDoctorScheduleService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IAppointmentService _appointmentService;

        public DoctorScheduleService(SmartHealthMonitoringContext context, IAppointmentService appointmentService)
        {
            _context = context;
            _appointmentService = appointmentService;
        }

        public async Task<Models.Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
        }

        public async Task<List<AppointmentSlot>> GetWeekSlotsAsync(int doctorId)
        {
            var today = SmartHealthMonitoring.Common.AppTime.Now.Date;
            var endDay = today.AddDays(7);

            return await _context.AppointmentSlots
                .Where(s => s.DoctorId == doctorId && s.SlotStart >= today && s.SlotStart < endDay)
                .OrderBy(s => s.SlotStart)
                .ToListAsync();
        }

        public async Task CleanupGhostSlotsAsync(int doctorId)
        {
            var today = SmartHealthMonitoring.Common.AppTime.Now.Date;
            var endDay = today.AddDays(7);

            var ghostSlots = await _context.AppointmentSlots
                .Where(s => s.DoctorId == doctorId && s.SlotStart >= endDay && s.Status == AppointmentSlotStatus.Available)
                .ToListAsync();
            
            if (ghostSlots.Any())
            {
                _context.AppointmentSlots.RemoveRange(ghostSlots);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<(bool Success, string? Error)> SaveScheduleAsync(int doctorId, List<DoctorSchedule7DaysDto> slots)
        {
            var today = SmartHealthMonitoring.Common.AppTime.Now.Date;
            var endDay = today.AddDays(7);

            var groupedSlots = slots.GroupBy(s => s.Date.Date);
            foreach (var group in groupedSlots)
            {
                var dailySlots = group.OrderBy(s => TimeOnly.Parse(s.StartTime)).ToList();
                for (int i = 0; i < dailySlots.Count; i++)
                {
                    var currentStart = TimeOnly.Parse(dailySlots[i].StartTime);
                    var currentEnd = TimeOnly.Parse(dailySlots[i].EndTime);

                    if (currentStart >= currentEnd)
                    {
                        return (false, "Gi? b?t d?u ph?i nh? hon gi? k?t th�c.");
                    }

                    if (i > 0)
                    {
                        var prevEnd = TimeOnly.Parse(dailySlots[i - 1].EndTime);
                        if (currentStart < prevEnd)
                        {
                            return (false, $"Ph�t hi?n tr�ng l?p th?i gian l�m vi?c v�o ng�y {dailySlots[i].Date:dd/MM}.");
                        }
                    }
                }
            }

            var existingSlots = await _context.AppointmentSlots
                .Where(s => s.DoctorId == doctorId && s.SlotStart >= today && s.SlotStart < endDay)
                .ToListAsync();

            var nonAvailableSlots = existingSlots.Where(s => s.Status == AppointmentSlotStatus.Booked || s.Status == AppointmentSlotStatus.SoftLocked).ToList();

            foreach (var bookedSlot in nonAvailableSlots)
            {
                var bookedDate = bookedSlot.SlotStart.Date;
                var bookedStartTime = TimeOnly.FromDateTime(bookedSlot.SlotStart);
                var bookedEndTime = TimeOnly.FromDateTime(bookedSlot.SlotEnd);

                var submittedBlocksForDate = groupedSlots.FirstOrDefault(g => g.Key == bookedDate)?.ToList() ?? new List<DoctorSchedule7DaysDto>();
                
                bool isCovered = false;
                foreach (var block in submittedBlocksForDate)
                {
                    var blockStart = TimeOnly.Parse(block.StartTime);
                    var blockEnd = TimeOnly.Parse(block.EndTime);
                    if (bookedStartTime >= blockStart && bookedEndTime <= blockEnd)
                    {
                        isCovered = true;
                        break;
                    }
                }

                if (!isCovered)
                {
                    return (false, $"�� c� b?nh nh�n d?t l?ch l�c {bookedSlot.SlotStart:HH:mm} ng�y {bookedSlot.SlotStart:dd/MM}. B?n kh�ng th? x�a ho?c thay d?i khung gi? n�y!");
                }
            }

            var deletableSlots = existingSlots.Where(s => s.Status == AppointmentSlotStatus.Available || s.Status == AppointmentSlotStatus.Blocked).ToList();
            _context.AppointmentSlots.RemoveRange(deletableSlots);

            foreach (var block in slots)
            {
                var date = block.Date.Date;
                var current = TimeOnly.Parse(block.StartTime).ToTimeSpan();
                var end = TimeOnly.Parse(block.EndTime).ToTimeSpan();
                var duration = TimeSpan.FromMinutes(block.SlotDurationMinutes);

                while (current + duration <= end)
                {
                    var slotStart = date.Add(current);
                    var slotEnd = slotStart.Add(duration);

                    bool exists = nonAvailableSlots.Any(s => s.SlotStart == slotStart);
                    if (!exists)
                    {
                        _context.AppointmentSlots.Add(new AppointmentSlot
                        {
                            DoctorId = doctorId,
                            SlotStart = slotStart,
                            SlotEnd = slotEnd,
                            Status = AppointmentSlotStatus.Available
                        });
                    }
                    current += duration;
                }
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}

