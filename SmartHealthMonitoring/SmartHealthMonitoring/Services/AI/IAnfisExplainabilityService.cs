using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI;

/// <summary>
/// Kết quả trả về từ lớp XAI (Explainable AI) của ANFIS.
/// </summary>
public class ExplainabilityResult
{
    /// <summary>Đoạn văn bản tổng hợp kết luận đầy đủ để bác sĩ đọc.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Danh sách các yếu tố nguy cơ được phát hiện.</summary>
    public List<string> RiskFactors { get; set; } = new();

    /// <summary>Danh sách các yếu tố bảo vệ (chỉ số tốt).</summary>
    public List<string> Protective { get; set; } = new();

    /// <summary>Nguồn dữ liệu được dùng để phân tích.</summary>
    public string DataSource { get; set; } = string.Empty;
}

/// <summary>
/// Interface cho XAI Wrapper Service của ANFIS.
/// </summary>
public interface IAnfisExplainabilityService
{
    /// <summary>
    /// Phân tích cảnh báo và tạo ra lời giải thích dạng text theo phong cách Y khoa.
    /// </summary>
    ExplainabilityResult Explain(AiriskPrediction prediction, WarningAlert alert);
}
