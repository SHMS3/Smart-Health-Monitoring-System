using Microsoft.ML.OnnxRuntime;

namespace SmartHealthMonitoring.Services.AI;

/// <summary>
/// Interface cho Singleton Service quản lý vòng đời của các ONNX InferenceSession
/// và dictionary lambda Box-Cox. Mô hình được nạp một lần duy nhất khi ứng dụng khởi động,
/// tái sử dụng cho mọi request (thread-safe).
/// </summary>
public interface IAiModelSessionRunner : IDisposable
{
    /// <summary>
    /// Lấy InferenceSession tương ứng với loại mô hình.
    /// </summary>
    /// <param name="modelType">Loại mô hình: "KNN" hoặc "SVM" (mặc định SVM)</param>
    InferenceSession GetSession(string modelType);

    /// <summary>
    /// Từ điển lambda của phép biến đổi Box-Cox, được nạp từ boxcox_lambdas.json.
    /// Key: tên feature (age, trestbps, chol, thalach, oldpeak).
    /// Value: giá trị lambda tương ứng.
    /// </summary>
    IReadOnlyDictionary<string, float> BoxCoxLambdas { get; }
}
