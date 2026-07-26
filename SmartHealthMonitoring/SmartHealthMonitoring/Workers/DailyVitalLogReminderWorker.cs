using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;

namespace SmartHealthMonitoring.Workers
{
    /// <summary>
    /// Background Worker chạy định kỳ, kiểm tra nếu bệnh nhân sau 1 giờ không ghi log
    /// thì gửi email nhắc nhở cho bệnh nhân đó.
    /// </summary>
    public class DailyVitalLogReminderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyVitalLogReminderWorker> _logger;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(1);

        public DailyVitalLogReminderWorker(IServiceProvider serviceProvider, ILogger<DailyVitalLogReminderWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyVitalLogReminderWorker bắt đầu chạy.");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await CheckAndSendRemindersAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not TaskCanceledException)
                    {
                        _logger.LogError(ex, "Lỗi xảy ra trong quá trình chạy DailyVitalLogReminderWorker.");
                    }

                    await Task.Delay(_period, stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("DailyVitalLogReminderWorker đang dừng lại do hệ thống tắt.");
            }
        }

        private async Task CheckAndSendRemindersAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
            var emailTriggerService = scope.ServiceProvider.GetRequiredService<IEmailTriggerService>();

            var now = DateTime.Now;
            var oneHourAgo = now.AddHours(-1);

            // Lấy danh sách bệnh nhân hoạt động
            var patients = await dbContext.Patients
                .Include(p => p.User)
                .Where(p => !p.IsDeleted && p.User != null && !p.User.IsDeleted && p.User.Role == 0)
                .ToListAsync(stoppingToken);

            _logger.LogInformation($"[DailyVitalLogReminderWorker] Đang quét {patients.Count} bệnh nhân.");

            foreach (var patient in patients)
            {
                // Tìm log gần nhất của bệnh nhân
                var latestLog = await dbContext.DailyVitalLogs
                    .Where(l => l.PatientId == patient.Id && !l.IsDeleted)
                    .OrderByDescending(l => l.LoggedAt)
                    .FirstOrDefaultAsync(stoppingToken);
                

                DateTime baseTime;
                string lastLogTimeDisplay;

                if (latestLog != null)
                {
                    baseTime = latestLog.LoggedAt;
                    lastLogTimeDisplay = latestLog.LoggedAt.ToString("dd/MM/yyyy HH:mm:ss");
                }
                else
                {
                    baseTime = patient.User.CreatedAt;
                    lastLogTimeDisplay = "Chưa từng ghi nhận";
                }

                // Nếu thời gian kể từ log cuối cùng (hoặc ngày tạo) đã hơn 1 giờ
                if (baseTime < oneHourAgo)
                {
                    // Kiểm tra xem đã tạo thông báo nội bộ kể từ baseTime chưa
                    bool alreadySent = await dbContext.EmailNotifications
                        .AnyAsync(n => n.PatientId == patient.Id
                                       && n.Status == 3
                                       && n.CreatedAt > baseTime, 
                                  stoppingToken);

                    if (!alreadySent)
                    {
                        _logger.LogInformation($"[DailyVitalLogReminderWorker] Tạo thông báo nhắc ghi log cho bệnh nhân {patient.User.FullName} (Id: {patient.Id}). Thời gian mốc: {lastLogTimeDisplay}");
                        await emailTriggerService.SendDailyVitalLogReminderAsync(patient.Id, lastLogTimeDisplay);
                    }
                }
            }
        }
    }
}
