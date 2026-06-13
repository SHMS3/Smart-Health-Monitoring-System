using Microsoft.AspNetCore.Hosting;
using Microsoft.ML.OnnxRuntime;
using System.Text.Json;

namespace SmartHealthMonitoring.Services.AI;

/// <summary>
/// Singleton Service nạp và quản lý các ONNX InferenceSession cùng với Box-Cox lambdas.
/// Tất cả được nạp một lần từ wwwroot/models/ khi ứng dụng khởi động.
/// InferenceSession là thread-safe, có thể tái sử dụng cho nhiều request đồng thời.
/// </summary>
public class AiModelSessionRunner : IAiModelSessionRunner
{
    private readonly InferenceSession _knnSession;
    private readonly InferenceSession _svmSession;
    private readonly InferenceSession? _anfisSession; // Nullable phòng trường hợp file chưa kịp chép vào
    private bool _disposed;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, float> BoxCoxLambdas { get; }

    public AiModelSessionRunner(IWebHostEnvironment webHostEnvironment)
    {
        var modelsPath = Path.Combine(webHostEnvironment.WebRootPath, "models");

        // ── Nạp ONNX models ───────────────────────────────────────────────────
        var knnPath = Path.Combine(modelsPath, "heart_disease_KNN_model.onnx");
        var svmPath = Path.Combine(modelsPath, "heart_disease_SVM_model.onnx");
        var anfisPath = Path.Combine(modelsPath, "heart_disease_ANFIS_model.onnx");

        if (!File.Exists(knnPath))
            throw new FileNotFoundException($"Không tìm thấy file mô hình KNN. Đường dẫn: {knnPath}");

        if (!File.Exists(svmPath))
            throw new FileNotFoundException($"Không tìm thấy file mô hình SVM. Đường dẫn: {svmPath}");

        _knnSession = new InferenceSession(knnPath);
        _svmSession = new InferenceSession(svmPath);
        
        if (File.Exists(anfisPath))
        {
            _anfisSession = new InferenceSession(anfisPath);
        }
        else
        {
            // Bỏ qua nếu chưa có file ANFIS, cho phép ứng dụng khởi động an toàn
            _anfisSession = null; 
        }

        // ── Nạp Box-Cox lambdas từ JSON ───────────────────────────────────────
        var lambdaPath = Path.Combine(modelsPath, "boxcox_lambdas.json");

        if (!File.Exists(lambdaPath))
            throw new FileNotFoundException($"Không tìm thấy file Box-Cox lambdas. Đường dẫn: {lambdaPath}");

        var json = File.ReadAllText(lambdaPath);

        // Deserialize sang Dictionary<string, double> trước, rồi cast sang float
        // để tránh mất độ chính xác khi System.Text.Json tự convert number thành float
        var doubleLambdas = JsonSerializer.Deserialize<Dictionary<string, double>>(json)
            ?? throw new InvalidOperationException("File boxcox_lambdas.json rỗng hoặc không hợp lệ.");

        BoxCoxLambdas = doubleLambdas
            .ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
    }

    /// <summary>
    /// Lấy InferenceSession tương ứng với loại mô hình yêu cầu.
    /// Mặc định trả về KNN.
    /// </summary>
    public InferenceSession GetSession(string modelType)
    {
        return modelType.ToUpperInvariant() switch
        {
            "KNN" => _knnSession,
            "SVM" => _svmSession,
            "ANFIS" => _anfisSession ?? throw new InvalidOperationException("Mô hình ANFIS chưa được tải vì thiếu file."),
            _     => _knnSession
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _knnSession?.Dispose();
        _svmSession?.Dispose();
        _anfisSession?.Dispose();
        _disposed = true;
    }
}
