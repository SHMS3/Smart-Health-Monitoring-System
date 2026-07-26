using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Workers;

/// <summary>
/// Background Worker tự động sinh AppointmentSlot 14 ngày tới
/// dựa trên DoctorWorkSchedule.
/// Chạy: lúc khởi động ứng dụng, và mỗi ngày lúc 00:05.
/// </summary>
public class AppointmentSlotGeneratorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentSlotGeneratorWorker> _logger;

    public AppointmentSlotGeneratorWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentSlotGeneratorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy ngay khi khởi động
        try
        {
            await GenerateSlotsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SlotGenerator] Lỗi xảy ra khi sinh slots lúc khởi động. Tiếp tục chạy worker...");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Tính thời gian đến 00:05 ngày hôm sau
            var now     = DateTime.UtcNow;
            var nextRun = DateTime.UtcNow.Date.AddDays(1).AddMinutes(5);
            var delay   = nextRun - now;

            _logger.LogInformation("[SlotGenerator] Next run in {Hours}h {Minutes}m.", (int)delay.TotalHours, delay.Minutes);
            await Task.Delay(delay, stoppingToken);
            try
            {
                await GenerateSlotsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SlotGenerator] Lỗi xảy ra khi sinh slots định kỳ.");
            }
        }
    }

    private async Task GenerateSlotsAsync(CancellationToken ct)
    {
        using var scope   = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();

        var today    = DateOnly.FromDateTime(AppointmentTime.NowVietnam);
        var genUntil = today.AddDays(3); // Tạo slot cho hôm nay + 3 ngày tới = 4 ngày

        var schedules = await context.DoctorWorkSchedules
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        int created = 0;
        var generatedSlots = new HashSet<(int DoctorId, DateTime SlotStart)>();

        foreach (var schedule in schedules)
        {
            // Duyệt qua 14 ngày, tìm ngày trùng DayOfWeek
            for (var d = today; d <= genUntil; d = d.AddDays(1))
            {
                if ((int)d.DayOfWeek != schedule.DayOfWeek) continue;

                // Sinh slot theo SlotDurationMinutes
                var current = schedule.StartTime;
                while (current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
                {
                    var slotStartLocal = d.ToDateTime(current);
                    var slotStart = AppointmentTime.ToUtc(slotStartLocal);
                    var slotEnd   = AppointmentTime.ToUtc(slotStartLocal.AddMinutes(schedule.SlotDurationMinutes));

                    // Kiểm tra slot đã tồn tại chưa (Unique Index sẽ bắt, nhưng check trước để tránh exception)
                    bool existsInDb = await context.AppointmentSlots
                        .AnyAsync(s => s.DoctorId == schedule.DoctorId && s.SlotStart == slotStart, ct);

                    var slotKey = (schedule.DoctorId, slotStart);
                    if (!existsInDb && !generatedSlots.Contains(slotKey))
                    {
                        generatedSlots.Add(slotKey);
                        context.AppointmentSlots.Add(new AppointmentSlot
                        {
                            DoctorId  = schedule.DoctorId,
                            SlotStart = slotStart,
                            SlotEnd   = slotEnd,
                            Status    = AppointmentSlotStatus.Available
                        });
                        created++;
                    }

                    current = current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));
                }
            }
        }

        if (created > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("[SlotGenerator] Generated {Count} new appointment slots.", created);
        }
    }
}
