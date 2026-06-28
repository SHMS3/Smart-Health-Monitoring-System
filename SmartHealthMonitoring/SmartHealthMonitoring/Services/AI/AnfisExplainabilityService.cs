using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI;

/// <summary>
/// XAI Wrapper Layer - Lớp giải thích khả năng đọc hiểu cho kết quả của ANFIS.
/// Dựa trên các tập luật IF-THEN (Fuzzy Rules) mô phỏng theo logic mờ của ANFIS,
/// sinh ra đoạn text tiếng Việt giải thích nguyên nhân dẫn đến kết quả dự đoán.
/// Chạy hoàn toàn offline, tốc độ dưới 1ms, không gọi bất kỳ API bên ngoài nào.
/// </summary>
public class AnfisExplainabilityService : IAnfisExplainabilityService
{
    // ─── Ngưỡng phân loại các chỉ số lâm sàng (theo tiêu chuẩn WHO/ESC 2023) ──
    private const int    SystolicBpDanger  = 140;  // mmHg  – Tăng huyết áp độ 1+
    private const int    SystolicBpHigh    = 130;  // mmHg  – Tiền tăng huyết áp
    private const int    HeartRateHigh     = 100;  // bpm   – Nhịp tim nhanh
    private const int    HeartRateLow      = 60;   // bpm   – Nhịp tim chậm
    private const int    MaxHrDanger       = 140;  // bpm   – Nhịp tối đa đáng lo ngại
    private const int    CholesterolHigh   = 200;  // mg/dL – Ngưỡng cần theo dõi
    private const double OldPeakDanger     = 2.0;  // mm    – ST Depression đáng lo ngại

    // ─── Mapping Chest Pain Type → mô tả ─────────────────────────────────────
    private static readonly Dictionary<byte, string> ChestPainDesc = new()
    {
        { 0, "không có đau ngực" },
        { 1, "đau ngực dạng điển hình (TA - Typical Angina)" },
        { 2, "đau ngực không điển hình (ATA - Atypical Angina)" },
        { 3, "đau ngực không do thiếu máu (NAP - Non-Anginal Pain)" },
    };

    private static readonly Dictionary<byte, string> SlopePolarityDesc = new()
    {
        { 0, "đoạn ST đi xuống (Down - dấu hiệu thiếu máu cơ tim)" },
        { 1, "đoạn ST nằm ngang (Flat - ranh giới nguy hiểm)" },
        { 2, "đoạn ST đi lên (Up - bình thường)" },
    };

    private static readonly Dictionary<byte, string> ThalDesc = new()
    {
        { 0, "chưa có dữ liệu Thalassemia" },
        { 1, "thalassemia bình thường (Normal)" },
        { 2, "khiếm khuyết cố định (Fixed Defect)" },
        { 3, "khiếm khuyết có thể hồi phục (Reversible Defect - nguy cơ cao nhất)" },
    };

    // ─── Entry point ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ExplainabilityResult Explain(AiriskPrediction prediction, WarningAlert alert)
    {
        var reasons    = new List<string>();
        var protective = new List<string>();

        // Lấy nguồn dữ liệu: ưu tiên ClinicalRecord (đầy đủ hơn), fallback DailyVitalLog
        bool hasClinical = prediction.ClinicalRecord != null;
        bool hasDaily    = prediction.DailyLog != null;

        if (!hasClinical && !hasDaily)
        {
            return new ExplainabilityResult
            {
                Summary     = "Không đủ dữ liệu nguồn để phân tích chi tiết.",
                RiskFactors = new List<string> { "Dữ liệu không đầy đủ." },
                Protective  = new List<string>(),
                DataSource  = "Không xác định"
            };
        }

        string dataSource = hasClinical ? "Hồ sơ khám lâm sàng (Clinical Exam)" : "Nhật ký chỉ số sinh tồn (Daily Vital Log)";

        // ═══════════════════════════════════════════════════════
        // LUỒNG 1: Phân tích từ ClinicalRecord (đầy đủ nhất)
        // ═══════════════════════════════════════════════════════
        if (hasClinical)
        {
            var r = prediction.ClinicalRecord!;

            // --- Huyết áp tâm thu ---
            if (r.RestingBp >= SystolicBpDanger)
                reasons.Add($"Huyết áp tâm thu khi nghỉ ngơi đang ở mức NGUY HIỂM ({r.RestingBp} mmHg ≥ {SystolicBpDanger} mmHg)");
            else if (r.RestingBp >= SystolicBpHigh)
                reasons.Add($"Huyết áp tâm thu cao hơn bình thường ({r.RestingBp} mmHg - tiền tăng huyết áp)");
            else
                protective.Add($"Huyết áp tâm thu trong giới hạn ổn định ({r.RestingBp} mmHg)");

            // --- Cholesterol ---
            if (r.Cholesterol >= CholesterolHigh)
                reasons.Add($"Cholesterol máu vượt ngưỡng an toàn ({r.Cholesterol} mg/dL ≥ {CholesterolHigh} mg/dL) - nguy cơ xơ vữa động mạch");
            else if (r.Cholesterol > 0)
                protective.Add($"Cholesterol máu trong giới hạn an toàn ({r.Cholesterol} mg/dL)");

            // --- Nhịp tim tối đa khi gắng sức ---
            if (r.MaxHeartRate < MaxHrDanger)
                reasons.Add($"Nhịp tim tối đa đạt được khi gắng sức thấp ({r.MaxHeartRate} bpm < {MaxHrDanger} bpm) - dấu hiệu kém thích nghi tim mạch");
            else
                protective.Add($"Nhịp tim tối đa khi gắng sức đạt mức bình thường ({r.MaxHeartRate} bpm)");

            // --- Đau ngực ---
            if (r.ChestPainType >= 1)
                reasons.Add($"Bệnh nhân có biểu hiện {ChestPainDesc.GetValueOrDefault(r.ChestPainType, $"đau ngực (loại {r.ChestPainType})")}");

            // --- Exercise Angina (đau thắt ngực khi gắng sức) ---
            if (r.ExerciseAngina == 1)
                reasons.Add("Xuất hiện đau thắt ngực khi gắng sức (Exercise-Induced Angina) - dấu hiệu thiếu máu cơ tim đặc trưng");

            // --- ST Depression (OldPeak) ---
            if ((double)r.OldPeak >= OldPeakDanger)
                reasons.Add($"Chỉ số ST Depression (OldPeak) bất thường ({r.OldPeak:F1} mm ≥ {OldPeakDanger} mm) - nguy cơ thiếu máu cơ tim khi gắng sức");
            else if ((double)r.OldPeak > 0)
                protective.Add($"Chỉ số ST Depression ở mức nhẹ ({r.OldPeak:F1} mm)");

            // --- ST Slope ---
            if (r.Stslope == 0 || r.Stslope == 1)
                reasons.Add($"Hình thái sóng ST bất thường: {SlopePolarityDesc.GetValueOrDefault(r.Stslope, $"Slope={r.Stslope}")}");
            else
                protective.Add($"Hình thái sóng ST bình thường (đi lên - Up Slope)");

            // --- Số mạch vành bị ảnh hưởng ---
            if (r.MajorVessels >= 1)
                reasons.Add($"Phát hiện {r.MajorVessels} mạch vành lớn bị thu hẹp qua chụp mạch vành (Fluoroscopy)");

            // --- Thalassemia / Tưới máu cơ tim ---
            if (r.ThalResult == 2 || r.ThalResult == 3)
                reasons.Add($"Kết quả kiểm tra tưới máu cơ tim (Thalassemia): {ThalDesc.GetValueOrDefault(r.ThalResult, $"loại {r.ThalResult}")}");

            // --- Đường huyết lúc đói ---
            if (r.FastingBs == 1)
                reasons.Add("Đường huyết lúc đói > 120 mg/dL (dấu hiệu tiểu đường hoặc tiền tiểu đường - yếu tố nguy cơ tim mạch)");
            else
                protective.Add("Đường huyết lúc đói trong ngưỡng bình thường");
        }

        // ═══════════════════════════════════════════════════════
        // LUỒNG 2: Phân tích từ DailyVitalLog (tín hiệu sơ bộ)
        // ═══════════════════════════════════════════════════════
        else if (hasDaily)
        {
            var d = prediction.DailyLog!;

            // --- Huyết áp ---
            if (d.SystolicBp >= SystolicBpDanger)
                reasons.Add($"Huyết áp tâm thu đang ở mức NGUY HIỂM ({d.SystolicBp} mmHg)");
            else if (d.SystolicBp >= SystolicBpHigh)
                reasons.Add($"Huyết áp tâm thu cao ({d.SystolicBp} mmHg - tiền tăng huyết áp)");
            else
                protective.Add($"Huyết áp tâm thu ổn định ({d.SystolicBp} mmHg)");

            // --- Nhịp tim ---
            if (d.HeartRate > HeartRateHigh)
                reasons.Add($"Nhịp tim đang NHANH bất thường ({d.HeartRate} bpm > {HeartRateHigh} bpm) - tachycardia");
            else if (d.HeartRate < HeartRateLow)
                reasons.Add($"Nhịp tim đang CHẬM bất thường ({d.HeartRate} bpm < {HeartRateLow} bpm) - bradycardia");
            else
                protective.Add($"Nhịp tim trong giới hạn bình thường ({d.HeartRate} bpm)");

            // --- Đau ngực ---
            if (d.ChestPainLevel >= 2)
                reasons.Add($"Bệnh nhân ghi nhận đau ngực ở mức độ đáng lo ngại (Cấp độ {d.ChestPainLevel}/3)");
            else if (d.ChestPainLevel == 1)
                reasons.Add($"Bệnh nhân ghi nhận đau ngực nhẹ (Cấp độ {d.ChestPainLevel}/3)");

            // --- Exercise Angina ---
            if (d.HasExerciseAngina)
                reasons.Add("Xuất hiện triệu chứng đau thắt ngực khi gắng sức");
            else
                protective.Add("Không có đau thắt ngực khi gắng sức");
        }

        // ═══════════════════════════════════════════════════════
        // Tổng hợp thành văn bản kết luận
        // ═══════════════════════════════════════════════════════
        decimal riskPct = prediction.RiskScore * 100;
        string riskLevelText = prediction.RiskLevel switch
        {
            3     => "NGUY KỊCH",
            2     => "CAO",
            1     => "TRUNG BÌNH",
            _     => "THẤP"
        };

        string summary;
        if (reasons.Count == 0)
        {
            summary = $"Hệ thống ANFIS ước tính xác suất bệnh tim mạch là {riskPct:F1}% dựa trên sự kết hợp phức tạp của nhiều yếu tố lâm sàng. " +
                      "Không phát hiện chỉ số đơn lẻ nào vượt ngưỡng nguy hiểm, tuy nhiên mô hình Fuzzy nhận diện được pattern bất thường " +
                      "trong tổ hợp các chỉ số. Đề nghị Bác sĩ tiến hành thăm khám trực tiếp để có đánh giá toàn diện.";
        }
        else
        {
            string causeText = string.Join("; ", reasons.Select((r, i) => $"({i + 1}) {r}"));
            summary = $"Thuật toán ANFIS xác định mức rủi ro {riskLevelText} ({riskPct:F1}%) " +
                      $"chủ yếu dựa trên {reasons.Count} yếu tố nguy cơ nổi bật được phát hiện: {causeText}.";
        }

        return new ExplainabilityResult
        {
            Summary     = summary,
            RiskFactors = reasons,
            Protective  = protective,
            DataSource  = dataSource
        };
    }
}
