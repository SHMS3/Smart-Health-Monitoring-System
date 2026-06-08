using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services.AI;

namespace SmartHealthMonitoring.Workers.AI;

/// <summary>
/// Background Worker chạy định kỳ, quét DailyVitalLogs và ClinicalRecords chưa được dự đoán,
/// gọi AI prediction service và tạo WarningAlert khi RiskLevel >= 2.
/// </summary>
public class AiPredictionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiPredictionWorker> _logger;
    //private readonly TimeSpan _period = TimeSpan.FromMinutes(1); // Chu kỳ quét production
    private readonly TimeSpan _period = TimeSpan.FromSeconds(20); // Dev: 20s

    public AiPredictionWorker(IServiceProvider serviceProvider, ILogger<AiPredictionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiPredictionWorker bat dau chay.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi xay ra trong qua trinh chay AiPredictionWorker.");
            }

            await Task.Delay(_period, stoppingToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiPredictionService>();

        int successCount = 0;
        int alertCount   = 0;

        // ═══════════════════════════════════════════════════════════════════════
        // LUONG 1: Quet DailyVitalLogs chua co du bao
        // → Ket hop voi ClinicalRecord gan nhat cua cung benh nhan de dua ra du doan
        // ═══════════════════════════════════════════════════════════════════════
        _logger.LogInformation("========== [LUONG 1] Bat dau quet DailyVitalLogs ==========");

        var pendingDailyLogs = await dbContext.DailyVitalLogs
            .Include(d => d.Patient)
            .Where(d => !d.IsDeleted && !d.AiriskPredictions.Any(a => !a.IsDeleted))
            .ToListAsync(stoppingToken);

        if (pendingDailyLogs.Count > 0)
        {
            _logger.LogInformation("[LUONG 1] Tim thay {Count} DailyVitalLog chua duoc du bao.", pendingDailyLogs.Count);

            foreach (var log in pendingDailyLogs)
            {
                try
                {
                    var latestClinicalRecord = await dbContext.ClinicalRecords
                        .Where(c => c.PatientId == log.PatientId && !c.IsDeleted)
                        .OrderByDescending(c => c.VisitDate)
                        .FirstOrDefaultAsync(stoppingToken);

                    // Bỏ qua ClinicalRecord nếu đã cũ hơn 3 tháng (90 ngày)
                    if (latestClinicalRecord != null)
                    {
                        var daysDiff = (log.LoggedAt - latestClinicalRecord.VisitDate).TotalDays;
                        if (daysDiff > 90)
                        {
                            _logger.LogWarning(
                                "ClinicalRecord {RecordId} cua Patient {PatientId} da qua han ({Days} ngay). Bo qua ket hop.",
                                latestClinicalRecord.Id, log.PatientId, Math.Round(daysDiff));
                            latestClinicalRecord = null;
                        }
                    }

                    var prediction = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "KNN");

                    prediction.PatientId         = log.PatientId;
                    prediction.DailyLogId        = log.Id;
                    prediction.ClinicalRecordId  = latestClinicalRecord?.Id;

                    dbContext.AiriskPredictions.Add(prediction);

                    // ── LOG KẾT QUẢ DỰ ĐOÁN ────────────────────────────────────
                    string diseaseStatus1 = prediction.PredictedTarget == 1 ? "CO BENH" : "KHONG BENH";
                    string riskLevelName1 = prediction.RiskLevel switch
                    {
                        3 => "CAO (High/Critical)",
                        2 => "TRUNG BINH (Medium)",
                        _ => "THAP (Low)"
                    };

                    _logger.LogInformation(
                        "[LUONG 1] --- KET QUA DU BAO ---\n" +
                        "  BenhNhan       : {PatientId}\n" +
                        "  DailyLog       : {LogId}\n" +
                        "  ClinicalRecord : {HasClinical}\n" +
                        "  KetQua         : {Status}\n" +
                        "  RiskScore      : {ScoreRaw:F4} ({ScorePct:P1})\n" +
                        "  RiskLevel      : {Level}\n" +
                        "  Model          : {Model}",
                        log.PatientId,
                        log.Id,
                        latestClinicalRecord != null ? $"Co (ID={latestClinicalRecord.Id})" : "Khong (dung fallback)",
                        diseaseStatus1,
                        (double)prediction.RiskScore,
                        (double)prediction.RiskScore,
                        riskLevelName1,
                        prediction.ModelVersion);

                    if (prediction.RiskLevel >= 2)
                    {
                        var alert = new WarningAlert
                        {
                            PatientId = log.PatientId,
                            Prediction = prediction,
                            Status     = 0,
                            FlaggedAt  = DateTime.Now,
                            IsDeleted  = false
                        };
                        dbContext.WarningAlerts.Add(alert);
                        alertCount++;
                        _logger.LogWarning(
                            "[LUONG 1] => TAO CANH BAO MOI (RiskLevel={Level}, RiskScore={Score:F4}) cho BenhNhan={PatientId}",
                            prediction.RiskLevel, (double)prediction.RiskScore, log.PatientId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[LUONG 1] => BO QUA: RiskLevel={Level} < 2 (RiskScore={Score:F4} < 40%), khong du nguong canh bao.",
                            prediction.RiskLevel, (double)prediction.RiskScore);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Loi khi xu ly DailyVitalLog ID {LogId}", log.Id);
                }
            }
        }
        else
        {
            _logger.LogInformation("[LUONG 1] Khong co DailyVitalLog moi nao can du bao.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LUONG 2: Quet ClinicalRecords chua co du bao
        // → Chi chay khi benh nhan KHONG co DailyLog nao
        // ═══════════════════════════════════════════════════════════════════════
        _logger.LogInformation("========== [LUONG 2] Bat dau quet ClinicalRecords ==========");

        var pendingClinicalRecords = await dbContext.ClinicalRecords
            .Include(c => c.Patient)
            .Where(c => !c.IsDeleted && !c.AiriskPredictions.Any(a => !a.IsDeleted))
            .ToListAsync(stoppingToken);

        if (pendingClinicalRecords.Count > 0)
        {
            _logger.LogInformation("[LUONG 2] Tim thay {Count} ClinicalRecord chua duoc du bao.", pendingClinicalRecords.Count);

            foreach (var record in pendingClinicalRecords)
            {
                try
                {
                    var prediction = aiService.PredictHeartDiseaseRisk(record, "KNN");

                    prediction.PatientId        = record.PatientId;
                    prediction.ClinicalRecordId = record.Id;
                    prediction.DailyLogId       = null;

                    dbContext.AiriskPredictions.Add(prediction);

                    // ── LOG KẾT QUẢ DỰ ĐOÁN ────────────────────────────────────
                    string diseaseStatus2 = prediction.PredictedTarget == 1 ? "CO BENH" : "KHONG BENH";
                    string riskLevelName2 = prediction.RiskLevel switch
                    {
                        3 => "CAO (High/Critical)",
                        2 => "TRUNG BINH (Medium)",
                        _ => "THAP (Low)"
                    };

                    _logger.LogInformation(
                        "[LUONG 2] --- KET QUA DU BAO ---\n" +
                        "  BenhNhan  : {PatientId}\n" +
                        "  Record    : {RecordId}\n" +
                        "  KetQua    : {Status}\n" +
                        "  RiskScore : {ScoreRaw:F4} ({ScorePct:P1})\n" +
                        "  RiskLevel : {Level}\n" +
                        "  Model     : {Model}",
                        record.PatientId,
                        record.Id,
                        diseaseStatus2,
                        (double)prediction.RiskScore,
                        (double)prediction.RiskScore,
                        riskLevelName2,
                        prediction.ModelVersion);

                    if (prediction.RiskLevel >= 2)
                    {
                        var alert = new WarningAlert
                        {
                            PatientId  = record.PatientId,
                            Prediction = prediction,
                            Status     = 0,
                            FlaggedAt  = DateTime.Now,
                            IsDeleted  = false
                        };
                        dbContext.WarningAlerts.Add(alert);
                        alertCount++;
                        _logger.LogWarning(
                            "[LUONG 2] => TAO CANH BAO MOI (RiskLevel={Level}, RiskScore={Score:F4}) cho BenhNhan={PatientId}",
                            prediction.RiskLevel, (double)prediction.RiskScore, record.PatientId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[LUONG 2] => BO QUA: RiskLevel={Level} < 2 (RiskScore={Score:F4} < 40%), khong du nguong canh bao.",
                            prediction.RiskLevel, (double)prediction.RiskScore);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Loi khi xu ly ClinicalRecord ID {RecordId}", record.Id);
                }
            }
        }
        else
        {
            _logger.LogInformation("[LUONG 2] Khong co ClinicalRecord moi nao can du bao.");
        }

        if (successCount > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        int total = pendingDailyLogs.Count + pendingClinicalRecords.Count;

        _logger.LogWarning(
            "\n" +
            "╔══════════════════════════════════════════════════════╗\n" +
            "║          AI PREDICTION WORKER - TONG KET             ║\n" +
            "╠══════════════════════════════════════════════════════╣\n" +
            "║  Thanh cong : {SuccessCount}/{Total,-6}                               ║\n" +
            "║  DailyLog   : {DailyCount,-6}                                 ║\n" +
            "║  Clinical   : {ClinicalCount,-6}                                 ║\n" +
            "║  Alert moi  : {AlertCount,-6}                                 ║\n" +
            "╚══════════════════════════════════════════════════════╝",
            successCount, total, pendingDailyLogs.Count, pendingClinicalRecords.Count, alertCount);
    }
}
