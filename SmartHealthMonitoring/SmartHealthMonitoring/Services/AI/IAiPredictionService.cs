using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI;

/// <summary>
/// Interface cho Scoped Service đảm nhận logic map dữ liệu bệnh nhân sang Tensor và gọi mô hình ONNX để dự đoán.
/// </summary>
public interface IAiPredictionService
{
    /// <summary>
    /// Dự đoán nguy cơ mắc bệnh tim dựa trên hồ sơ lâm sàng (ClinicalRecord) — dùng khi không có DailyLog.
    /// </summary>
    AiriskPrediction PredictHeartDiseaseRisk(ClinicalRecord record, string modelType = "SVM");

    /// <summary>
    /// Dự đoán kết hợp: lấy chỉ số sinh hiệu mới nhất từ DailyVitalLog (SystolicBp, HeartRate, ChestPainLevel, ExerciseAngina)
    /// và các chỉ số xét nghiệm từ ClinicalRecord gần nhất (Cholesterol, FastingBS, OldPeak, STSlope...).
    /// ClinicalRecord có thể null nếu bệnh nhân chưa có lần khám nào.
    /// </summary>
    AiriskPrediction PredictCombined(DailyVitalLog log, ClinicalRecord? clinicalRecord, Patient patient, string modelType = "SVM", IReadOnlyList<string>? purchasedServiceNames = null);
}
