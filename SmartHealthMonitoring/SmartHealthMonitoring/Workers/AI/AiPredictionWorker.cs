using SmartHealthMonitoring.Interfaces.AI;
using SmartHealthMonitoring.Interfaces.Email;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services.AI;

namespace SmartHealthMonitoring.Workers.AI;

public class AiPredictionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiPredictionWorker> _logger;
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
        var dbContext  = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
        var aiService  = scope.ServiceProvider.GetRequiredService<IAiPredictionService>();
        var emailTriggerService = scope.ServiceProvider.GetRequiredService<IEmailTriggerService>();

        int successCount = 0;
        int alertCount   = 0;
        var alertEmailCandidates = new List<(int PatientId, AiriskPrediction Prediction)>();

        // Catch up alerts created or promoted directly in the database. Those changes
        // bypass the in-memory candidate list below, so their email side effect must
        // be dispatched separately.
        await DispatchPendingHealthWarningEmailsAsync(
            dbContext,
            emailTriggerService,
            stoppingToken);
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

                    var predKNN = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "KNN");
                    var predSVM = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "SVM");
                    AiriskPrediction? predANFIS = null;
                    try
                    {
                        predANFIS = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "ANFIS");
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    var prediction = new AiriskPrediction
                    {
                        PredictedAt = DateTime.Now,
                        IsDeleted = false
                    };

                    decimal avgScore;
                    if (predANFIS != null)
                    {
                        avgScore = (predKNN.RiskScore + predSVM.RiskScore + predANFIS.RiskScore) / 3;
                        prediction.ModelVersion = "ENS_KSA_1.0"; // KNN+SVM+ANFIS (max 20 chars)
                        _logger.LogInformation(
                            "[LUONG 1] Ensemble 3AI: KNN={KNN:F4}, SVM={SVM:F4}, ANFIS={ANFIS:F4} => AVG={AVG:F4}",
                            (double)predKNN.RiskScore, (double)predSVM.RiskScore, (double)predANFIS.RiskScore, (double)avgScore);
                    }
                    else
                    {
                        avgScore = (predKNN.RiskScore + predSVM.RiskScore) / 2;
                        prediction.ModelVersion = "ENS_KS_1.0"; // KNN+SVM only (max 20 chars)
                        _logger.LogInformation(
                            "[LUONG 1] Ensemble 2AI: KNN={KNN:F4}, SVM={SVM:F4} => AVG={AVG:F4}",
                            (double)predKNN.RiskScore, (double)predSVM.RiskScore, (double)avgScore);
                    }

                    decimal clinicalAdj = 0m;

                    short sbp = log.SystolicBp;
                    short rhr = log.HeartRate;
                    byte  cp  = log.ChestPainLevel;
                    bool  ex  = log.HasExerciseAngina;

                    if      (sbp >= 180) clinicalAdj += 0.32m; // Hypertensive Crisis
                    else if (sbp >= 160) clinicalAdj += 0.22m; // Stage 2 nang
                    else if (sbp >= 140) clinicalAdj += 0.15m; // Stage 2
                    else if (sbp >= 130) clinicalAdj += 0.08m; // Stage 1
                    else if (sbp >= 120) clinicalAdj += 0.03m; // Elevated (tren binh thuong)

                    if      (rhr >= 130) clinicalAdj += 0.28m; // Tachycardia nghiem trong
                    else if (rhr >= 110) clinicalAdj += 0.18m; // Tachycardia vua
                    else if (rhr >= 100) clinicalAdj += 0.12m; // Tachycardia nhe
                    else if (rhr >=  90) clinicalAdj += 0.06m; // Nhip hoi cao
                    else if (rhr >=  85) clinicalAdj += 0.02m; // Nhip gioi han tren binh thuong

                    if      (cp >= 3) clinicalAdj += 0.20m; // Dau nang (Typical Angina tuong duong)
                    else if (cp >= 2) clinicalAdj += 0.12m; // Dau vua (Atypical Angina tuong duong)
                    else if (cp >= 1) clinicalAdj += 0.05m; // Dau nhe

                    if (ex) clinicalAdj += 0.15m;

                    int riskFactorCount = 0;
                    if (sbp >= 130) riskFactorCount++;
                    if (rhr >= 90)  riskFactorCount++;
                    if (cp  >= 1)   riskFactorCount++;
                    if (ex)         riskFactorCount++;
                    if (riskFactorCount >= 3) clinicalAdj += 0.12m; // 3+ yeu to nguy co -> cong them
                    else if (riskFactorCount >= 2) clinicalAdj += 0.05m; // 2 yeu to -> cong nhe

                    if (clinicalAdj > 0m)
                    {
                        decimal scoreBeforeAdj = avgScore;
                        avgScore = Math.Min(1m, avgScore + clinicalAdj);
                        _logger.LogInformation(
                            "[LUONG 1] CLINICAL ADJ: SBP={SBP}mmHg, HR={HR}bpm, CP={CP}, ExAngina={Ex}, RiskFactors={RF} => +{Adj:F2} ({Before:F4} -> {After:F4})",
                            sbp, rhr, cp, ex, riskFactorCount, (double)clinicalAdj, (double)scoreBeforeAdj, (double)avgScore);
                    }

                    prediction.RiskScore = avgScore;
                    prediction.PredictedTarget = (byte)(avgScore >= 0.5m ? 1 : 0);
                    if (avgScore >= 0.8m)
                        prediction.RiskLevel = 3;
                    else if (avgScore >= 0.4m)
                        prediction.RiskLevel = 2;
                    else
                        prediction.RiskLevel = 1;

                    prediction.PatientId         = log.PatientId;
                    prediction.DailyLogId        = log.Id;
                    prediction.ClinicalRecordId  = latestClinicalRecord?.Id;

                    dbContext.AiriskPredictions.Add(prediction);

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

                        alertEmailCandidates.Add((log.PatientId, prediction));

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
                    var predKNN = aiService.PredictHeartDiseaseRisk(record, "KNN");
                    var predSVM = aiService.PredictHeartDiseaseRisk(record, "SVM");
                    AiriskPrediction? predANFIS = null;
                    try
                    {
                        predANFIS = aiService.PredictHeartDiseaseRisk(record, "ANFIS");
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    var prediction = new AiriskPrediction
                    {
                        PredictedAt = DateTime.Now,
                        IsDeleted = false
                    };

                    decimal avgScore;
                    if (predANFIS != null)
                    {
                        avgScore = (predKNN.RiskScore + predSVM.RiskScore + predANFIS.RiskScore) / 3;
                        prediction.ModelVersion = "ENS_KSA_1.0"; // KNN+SVM+ANFIS (max 20 chars)
                        _logger.LogInformation(
                            "[LUONG 2] Ensemble 3AI: KNN={KNN:F4}, SVM={SVM:F4}, ANFIS={ANFIS:F4} => AVG={AVG:F4}",
                            (double)predKNN.RiskScore, (double)predSVM.RiskScore, (double)predANFIS.RiskScore, (double)avgScore);
                    }
                    else
                    {
                        avgScore = (predKNN.RiskScore + predSVM.RiskScore) / 2;
                        prediction.ModelVersion = "ENS_KS_1.0"; // KNN+SVM only (max 20 chars)
                        _logger.LogInformation(
                            "[LUONG 2] Ensemble 2AI: KNN={KNN:F4}, SVM={SVM:F4} => AVG={AVG:F4}",
                            (double)predKNN.RiskScore, (double)predSVM.RiskScore, (double)avgScore);
                    }

                    prediction.RiskScore = avgScore;
                    prediction.PredictedTarget = (byte)(avgScore >= 0.5m ? 1 : 0);
                    if (avgScore >= 0.8m)
                        prediction.RiskLevel = 3;
                    else if (avgScore >= 0.4m)
                        prediction.RiskLevel = 2;
                    else
                        prediction.RiskLevel = 1;

                    prediction.PatientId        = record.PatientId;
                    prediction.ClinicalRecordId = record.Id;
                    prediction.DailyLogId       = null;

                    dbContext.AiriskPredictions.Add(prediction);

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

                        alertEmailCandidates.Add((record.PatientId, prediction));

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

            foreach (var candidate in alertEmailCandidates)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var emailSent = await emailTriggerService.SendHealthWarningAsync(
                        candidate.PatientId,
                        candidate.Prediction.Id);

                    if (emailSent)
                    {
                        _logger.LogWarning(
                            "[EMAIL] Da gui mail canh bao cho tat ca nguoi nhan cua BenhNhan={PatientId}, Prediction={PredictionId}, RiskScore={RiskScore:P2}",
                            candidate.PatientId,
                            candidate.Prediction.Id,
                            (double)candidate.Prediction.RiskScore);
                    }
                    else
                    {
                        _logger.LogError(
                            "[EMAIL] Co it nhat mot nguoi nhan chua nhan duoc mail canh bao cua BenhNhan={PatientId}, Prediction={PredictionId}",
                            candidate.PatientId,
                            candidate.Prediction.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[EMAIL] Loi khi gui mail canh bao khan cap cho BenhNhan={PatientId}, Prediction={PredictionId}",
                        candidate.PatientId,
                        candidate.Prediction.Id);
                }
            }
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

    private async Task DispatchPendingHealthWarningEmailsAsync(
        SmartHealthMonitoringContext dbContext,
        IEmailTriggerService emailTriggerService,
        CancellationToken stoppingToken)
    {
        var pendingAlerts = await dbContext.WarningAlerts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(alert => alert.Prediction)
            .Include(alert => alert.Patient)
                .ThenInclude(patient => patient.User)
            .Include(alert => alert.Patient)
                .ThenInclude(patient => patient.EmergencyContacts)
            .Include(alert => alert.EmailNotifications)
            .Where(alert =>
                !alert.IsDeleted &&
                !alert.Prediction.IsDeleted &&
                alert.Prediction.RiskLevel >= 2 &&
                !alert.Patient.IsDeleted &&
                !alert.Patient.User.IsDeleted &&
                (!alert.EmailNotifications.Any(notification =>
                     notification.SentByDoctorId == null) ||
                 alert.EmailNotifications.Any(notification =>
                     notification.SentByDoctorId == null &&
                     !notification.IsSent &&
                     notification.Status == 0)))
            .OrderBy(alert => alert.FlaggedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        foreach (var alert in pendingAlerts)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var recipientEmails = new List<string>();
            if (!string.IsNullOrWhiteSpace(alert.Patient.User.Email))
            {
                recipientEmails.Add(alert.Patient.User.Email.Trim());
            }

            recipientEmails.AddRange(alert.Patient.EmergencyContacts
                .Where(contact =>
                    contact.IsActive &&
                    !contact.IsDeleted &&
                    !string.IsNullOrWhiteSpace(contact.Email))
                .Select(contact => contact.Email!.Trim()));

            var automaticNotifications = alert.EmailNotifications
                .Where(notification => notification.SentByDoctorId == null)
                .ToList();

            var needsDelivery = recipientEmails
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Any(recipientEmail =>
                {
                    var notifications = automaticNotifications
                        .Where(notification => string.Equals(
                            notification.ToEmail,
                            recipientEmail,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    return notifications.Count == 0 ||
                           notifications.Any(notification =>
                               !notification.IsSent && notification.Status == 0);
                });

            if (!needsDelivery)
            {
                continue;
            }

            try
            {
                await emailTriggerService.SendHealthWarningAsync(
                    alert.PatientId,
                    alert.PredictionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EMAIL] Loi khi gui bu mail canh bao cho Alert={AlertId}, Patient={PatientId}, Prediction={PredictionId}",
                    alert.Id,
                    alert.PatientId,
                    alert.PredictionId);
            }
        }
    }
}

