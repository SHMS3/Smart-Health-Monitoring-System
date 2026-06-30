using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Workers;

/// <summary>
/// Background Worker dọn dẹp tự động:
/// 1. Nhả các SoftLocked slot đã hết 5 phút → về Available
/// 2. Đánh dấu No-show nếu bệnh nhân không đến 15 phút sau giờ hẹn
/// Chạy mỗi 2 phút.
/// </summary>
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
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(_runInterval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope   = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
        var now     = DateTime.UtcNow;

        // ────────────────────────────────────────────────────────────
        // 1. Nhả SoftLocked slot đã hết hạn giữ chỗ → Available
        // ────────────────────────────────────────────────────────────
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

        // ────────────────────────────────────────────────────────────
        // 2. Đánh dấu No-show (bệnh nhân đặt lịch nhưng không đến 15 phút)
        // ────────────────────────────────────────────────────────────
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
            await context.SaveChangesAsync(ct);
    }
}
