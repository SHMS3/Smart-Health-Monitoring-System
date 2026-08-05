using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Hubs;

namespace SmartHealthMonitoring.Workers;

public class AppointmentCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentCleanupWorker> _logger;
    private static readonly TimeSpan _runInterval = TimeSpan.FromMinutes(2);

    public AppointmentCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình dọn dẹp lịch hẹn.");
            }
            await Task.Delay(_runInterval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope   = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
        var now     = SmartHealthMonitoring.Common.AppTime.Now;

        var expiredSoftLocks = await context.AppointmentSlots
            .Where(s => s.Status == AppointmentSlotStatus.SoftLocked && s.SoftLockedUntil < now)
            .ToListAsync(ct);

        foreach (var slot in expiredSoftLocks)
        {
            slot.Status          = AppointmentSlotStatus.Available;
            slot.PatientId       = null;
            slot.SoftLockedUntil = null;
        }

        if (expiredSoftLocks.Any())
            _logger.LogInformation("[Cleanup] Released {Count} expired SoftLocked slots.", expiredSoftLocks.Count);

        var noShowThreshold = now.AddMinutes(-15);

        var noShowAppointments = await context.Appointments
            .Include(a => a.Slot)
            .Where(a =>
                a.Status == AppointmentStatus.Confirmed &&
                a.Slot.SlotStart < noShowThreshold)
            .ToListAsync(ct);

        foreach (var appt in noShowAppointments)
        {
            appt.Status        = AppointmentStatus.NoShow;
            appt.UpdatedAt     = now;
            appt.Slot.Status   = AppointmentSlotStatus.Available;  // Giải phóng slot
            appt.Slot.PatientId = null;
        }

        if (noShowAppointments.Any())
            _logger.LogInformation("[Cleanup] Marked {Count} appointments as NoShow.", noShowAppointments.Count);

        if (expiredSoftLocks.Any() || noShowAppointments.Any())
        {
            await context.SaveChangesAsync(ct);
            try
            {
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppointmentHub>>();
                foreach (var slot in expiredSoftLocks)
                {
                    await hubContext.Clients.All.SendAsync("SlotStatusChanged", slot.Id, "Available");
                }
                foreach (var appt in noShowAppointments)
                {
                    await hubContext.Clients.All.SendAsync("SlotStatusChanged", appt.SlotId, "Available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting slot cleanup updates.");
            }
        }
    }
}
