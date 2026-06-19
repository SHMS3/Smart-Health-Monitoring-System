using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.Services.AI;

namespace SmartHealthMonitoring.Workers.AI;

/// <summary>
/// Background Worker chạy định kỳ, quét DailyVitalLogs và ClinicalRecords chưa được dự đoán,
/// gọi AI prediction service và tạo WarningAlert khi RiskLevel >= 2.
/// Khi không có bác sĩ nào đang trực (IsOnShift=true), kích hoạt Báo động đỏ toàn trạm.
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
        var dbContext  = scope.ServiceProvider.GetRequiredService<SmartHealthMonitoringContext>();
        var aiService  = scope.ServiceProvider.GetRequiredService<IAiPredictionService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

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

                    var predKNN = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "KNN");
                    var predSVM = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "SVM");
                    AiriskPrediction? predANFIS = null;
                    try
                    {
                        predANFIS = aiService.PredictCombined(log, latestClinicalRecord, log.Patient, "ANFIS");
                    }
                    catch (InvalidOperationException)
                    {
                        // File ANFIS chưa được thêm vào thư mục
                    }

                    var prediction = new AiriskPrediction
                    {
                        PredictedAt = DateTime.Now,
                        IsDeleted = false
                    };

                    // ── ENSEMBLE: Tính trung bình RiskScore từ nhiều mô hình ──
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

                    // DIEU CHINH LAM SANG - BU DAP CHO DU LIEU THIEU/DO TRE THOI GIAN (chi ap dung cho DailyVitalLog)
                    // Mo hinh UCI can 18 features (do cung 1 thoi diem). DailyLog chi co 5/18 features cap tinh.
                    // 13 features con lai (tu ho so kham cu hoac fallback 'khoe manh') se lam pha loang
                    // cac trieu chung cap tinh hien tai -> model de bi thien lech ve khong benh.
                    // Giai phap: Cong them bonus nguy co dua tren huong dan lam sang ACC/AHA 2017.
                    decimal clinicalAdj = 0m;

                    short sbp = log.SystolicBp;
                    short rhr = log.HeartRate;
                    byte  cp  = log.ChestPainLevel;
                    bool  ex  = log.HasExerciseAngina;

                    // (1) Huyet ap tam thu - ACC/AHA 2017 Hypertension Guidelines
                    if      (sbp >= 180) clinicalAdj += 0.32m; // Hypertensive Crisis
                    else if (sbp >= 160) clinicalAdj += 0.22m; // Stage 2 nang
                    else if (sbp >= 140) clinicalAdj += 0.15m; // Stage 2
                    else if (sbp >= 130) clinicalAdj += 0.08m; // Stage 1
                    else if (sbp >= 120) clinicalAdj += 0.03m; // Elevated (tren binh thuong)

                    // (2) Nhip tim nghi ngoi - AHA Resting Tachycardia
                    // Ngu?ng ha xuong 85 bpm de bat ca nhip tim hoi cao cua benh nhan tim
                    if      (rhr >= 130) clinicalAdj += 0.28m; // Tachycardia nghiem trong
                    else if (rhr >= 110) clinicalAdj += 0.18m; // Tachycardia vua
                    else if (rhr >= 100) clinicalAdj += 0.12m; // Tachycardia nhe
                    else if (rhr >=  90) clinicalAdj += 0.06m; // Nhip hoi cao
                    else if (rhr >=  85) clinicalAdj += 0.02m; // Nhip gioi han tren binh thuong

                    // (3) Muc do dau nguc - ghi nhan tu benh nhan
                    // Model da xu ly cp nhu feature nhung bi 13 fallback 'khoe' lam giam tai trong.
                    // Cong them bonus doc lap de dam bao dau nguc duoc the hien trong ket qua.
                    if      (cp >= 3) clinicalAdj += 0.20m; // Dau nang (Typical Angina tuong duong)
                    else if (cp >= 2) clinicalAdj += 0.12m; // Dau vua (Atypical Angina tuong duong)
                    else if (cp >= 1) clinicalAdj += 0.05m; // Dau nhe

                    // (4) Dau that nguc khi van dong - dau hieu lam sang quan trong
                    if (ex) clinicalAdj += 0.15m;

                    // (5) Bonus ket hop: nhieu yeu to nguy co cung luc -> nguy co nhan len
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

                        // KIEM TRA CA TRUC: neu khong co bac si nao online -> Thong bao khan cap cho benh nhan
                        await NotifyPatientIfNoDoctorOnShiftAsync(
                            dbContext, emailService, log.PatientId,
                            prediction.RiskScore, prediction.RiskLevel, stoppingToken);
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
                    var predKNN = aiService.PredictHeartDiseaseRisk(record, "KNN");
                    var predSVM = aiService.PredictHeartDiseaseRisk(record, "SVM");
                    AiriskPrediction? predANFIS = null;
                    try
                    {
                        predANFIS = aiService.PredictHeartDiseaseRisk(record, "ANFIS");
                    }
                    catch (InvalidOperationException)
                    {
                        // File ANFIS chưa được thêm vào thư mục
                    }

                    var prediction = new AiriskPrediction
                    {
                        PredictedAt = DateTime.Now,
                        IsDeleted = false
                    };

                    // ── ENSEMBLE: Tính trung bình RiskScore từ nhiều mô hình ──
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

                        // KIEM TRA CA TRUC: neu khong co bac si nao online -> Thong bao khan cap cho benh nhan
                        await NotifyPatientIfNoDoctorOnShiftAsync(
                            dbContext, emailService, record.PatientId,
                            prediction.RiskScore, prediction.RiskLevel, stoppingToken);
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

    /// <summary>
    /// Kiem tra co bac si nao dang truc khong.
    /// Neu khong co ai: gui email khan cap cho benh nhan yeu cau goi 115 di cap cuu.
    /// </summary>
    private async Task NotifyPatientIfNoDoctorOnShiftAsync(
        SmartHealthMonitoringContext dbContext,
        IEmailService emailService,
        int patientId,
        decimal riskScore,
        byte riskLevel,
        CancellationToken stoppingToken)
    {
        bool anyDoctorOnShift = await dbContext.Doctors
            .AnyAsync(d => d.IsOnShift && !d.IsDeleted, stoppingToken);

        if (anyDoctorOnShift)
        {
            _logger.LogInformation("[CANH BAO] Da co bac si dang truc. Khong can gui thong bao cho benh nhan.");
            return;
        }

        // Lay thong tin benh nhan de gui email
        var patient = await dbContext.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == patientId, stoppingToken);

        if (patient == null || patient.User == null || string.IsNullOrEmpty(patient.User.Email))
        {
            _logger.LogWarning("[BAO DONG DO] Khong tim thay email cua benh nhan {PatientId} de gui thong bao cap cuu!", patientId);
            return;
        }

        string patientEmail = patient.User.Email;
        string patientName = patient.User.FullName;
        string flaggedTime = DateTime.Now.ToString("HH:mm dd/MM/yyyy");

        _logger.LogWarning(
            "[BAO DONG DO] KHONG CO BAC SI NAO DANG TRUC! " +
            "Bat dau gui email yeu cau di cap cuu 115 cho BenhNhan={PatientId} ({Email}).",
            patientId, patientEmail);

        string subject = $"[KHẨN CẤP] CẢNH BÁO SỨC KHỎE NGUY HIỂM - YÊU CẦU ĐI CẤP CỨU NGAY!";
        string body = $"""
            <!DOCTYPE html><html><body style='font-family:Arial,sans-serif;background:#f8f9fa;padding:20px'>
            <div style='max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 15px rgba(0,0,0,.1)'>
              <div style='background:linear-gradient(135deg,#dc2626,#b91c1c);padding:30px 24px;text-align:center'>
                <div style='font-size:48px'>🚑</div>
                <h2 style='color:#fff;margin:12px 0 4px'>CẢNH BÁO Y TẾ KHẨN CẤP</h2>
                <p style='color:#fecaca;margin:0;font-size:15px'>Trạng thái sức khỏe nguy hiểm - Cần can thiệp y tế lập tức</p>
              </div>
              <div style='padding:28px 24px'>
                <p style='font-size:16px;color:#1f2937'>Kính gửi <strong>{patientName}</strong>,</p>
                <p style='font-size:16px;color:#374151;line-height:1.6'>
                  Hệ thống Trí tuệ Nhân tạo (AI) của chúng tôi vừa phân tích các chỉ số sức khỏe bạn gửi lúc <strong>{flaggedTime}</strong> 
                  và phát hiện các dấu hiệu <strong>CỰC KỲ NGUY HIỂM</strong> có nguy cơ đe dọa trực tiếp đến tính mạng.
                </p>
                
                <div style='background:#fff1f2;border-left:4px solid #e11d48;padding:16px;margin:24px 0;border-radius:0 8px 8px 0'>
                  <h3 style='margin:0 0 8px;color:#e11d48;font-size:18px'>⚠️ THÔNG BÁO QUAN TRỌNG:</h3>
                  <p style='margin:0;color:#881337;font-size:15px;line-height:1.5'>
                    Hiện tại phòng khám trực tuyến <strong>ĐANG NGOÀI GIỜ LÀM VIỆC</strong> và không có Bác sĩ trực ban để hỗ trợ ngay lập tức.
                  </p>
                </div>

                <h3 style='color:#1f2937;margin-bottom:12px'>HƯỚNG DẪN XỬ TRÍ NGAY LẬP TỨC:</h3>
                <ul style='color:#dc2626;font-size:16px;line-height:1.7;font-weight:bold;margin-bottom:24px'>
                  <li>KHÔNG chờ đợi bác sĩ phản hồi trên ứng dụng!</li>
                  <li>GỌI NGAY CHO CẤP CỨU 115.</li>
                  <li>HOẶC nhờ người nhà đưa đến Cơ sở Y tế / Bệnh viện gần nhất NGAY LẬP TỨC.</li>
                </ul>

                <p style='color:#4b5563;font-size:14px;font-style:italic'>
                  * Cảnh báo y tế này đã được lưu vào hồ sơ của bạn. Bác sĩ của chúng tôi sẽ liên hệ lại với bạn vào ca làm việc tiếp theo để theo dõi tiến triển.
                </p>
              </div>
              <div style='background:#f9fafb;padding:16px 24px;text-align:center;font-size:12px;color:#9ca3af;border-top:1px solid #f3f4f6'>
                Smart Health Monitoring System - Tin nhắn cảnh báo tự động sinh ra bởi AI.
              </div>
            </div></body></html>
            """;

        // Fire and Forget - khong block Worker
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendEmailAsync(patientEmail, subject, body);
                _logger.LogInformation("[BAO DONG DO] Đã gửi email cấp cứu 115 thành công tới bệnh nhân.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BAO DONG DO] Loi khi gui email cap cuu toi benh nhan {Email}", patientEmail);
            }
        });
    }
}
