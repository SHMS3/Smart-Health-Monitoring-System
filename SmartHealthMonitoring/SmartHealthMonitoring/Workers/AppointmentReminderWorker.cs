using SmartHealthMonitoring.Interfaces.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Workers;

public class AppointmentReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderWorker> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan WindowHalf = TimeSpan.FromMinutes(5);

    public AppointmentReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
   
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentReminderWorker bắt đầu chạy (mỗi 5 phút).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong AppointmentReminderWorker.");
            }

            try
            {
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("AppointmentReminderWorker đã dừng.");
    }

    private async Task ProcessRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
        var emailTrigger = scope.ServiceProvider.GetRequiredService<IEmailTriggerService>();

        var now = SmartHealthMonitoring.Common.AppTime.Now;

        await SendWindowRemindersAsync(
            context,
            emailTrigger,
            now,
            hoursBefore: 24,
            reminderLabel: "24 giờ",
            is24h: true,
            ct);

        await SendWindowRemindersAsync(
            context,
            emailTrigger,
            now,
            hoursBefore: 2,
            reminderLabel: "2 giờ",
            is24h: false,
            ct);
    }

    private async Task SendWindowRemindersAsync(
        SmartHealthMonitoringContext context,
        IEmailTriggerService emailTrigger,
        DateTime now,
        int hoursBefore,
        string reminderLabel,
        bool is24h,
        CancellationToken ct)
    {
        var windowStart = now.AddHours(hoursBefore).Subtract(WindowHalf);
        var windowEnd = now.AddHours(hoursBefore).Add(WindowHalf);

        var query = context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a =>
                a.Status == AppointmentStatus.Confirmed &&
                a.Slot.SlotStart >= windowStart &&
                a.Slot.SlotStart <= windowEnd);

        query = is24h
            ? query.Where(a => !a.IsReminded24h)
            : query.Where(a => !a.IsReminded2h);

        var appointments = await query.ToListAsync(ct);
        if (appointments.Count == 0)
            return;

        _logger.LogInformation(
            "[AppointmentReminder] Tìm thấy {Count} lịch hẹn cần nhắc {Label}.",
            appointments.Count,
            reminderLabel);

        foreach (var appointment in appointments)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await emailTrigger.SendAppointmentReminderAsync(appointment.Id, reminderLabel);

                if (is24h)
                    appointment.IsReminded24h = true;
                else
                    appointment.IsReminded2h = true;

                appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;
                await context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[AppointmentReminder] Đã nhắc {Label} cho appointment #{Id}.",
                    reminderLabel,
                    appointment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[AppointmentReminder] Lỗi khi nhắc {Label} cho appointment #{Id}.",
                    reminderLabel,
                    appointment.Id);
            }
        }
    }
}

