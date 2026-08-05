using SmartHealthMonitoring.Interfaces.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.ML.OnnxRuntime;
using System.Text.Json;

namespace SmartHealthMonitoring.Services.AI;

public class AiModelSessionRunner : IAiModelSessionRunner
{
    private readonly InferenceSession _knnSession;
    private readonly InferenceSession _svmSession;
    private readonly InferenceSession? _anfisSession; // Nullable phòng trường hợp file chưa kịp chép vào
    private bool _disposed;

    public IReadOnlyDictionary<string, float> BoxCoxLambdas { get; }

    public AiModelSessionRunner(IWebHostEnvironment webHostEnvironment)
    {
        var modelsPath = Path.Combine(webHostEnvironment.WebRootPath, "models");

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
            _anfisSession = null; 
        }

        var lambdaPath = Path.Combine(modelsPath, "boxcox_lambdas.json");

        if (!File.Exists(lambdaPath))
            throw new FileNotFoundException($"Không tìm thấy file Box-Cox lambdas. Đường dẫn: {lambdaPath}");

        var json = File.ReadAllText(lambdaPath);

        var doubleLambdas = JsonSerializer.Deserialize<Dictionary<string, double>>(json)
            ?? throw new InvalidOperationException("File boxcox_lambdas.json rỗng hoặc không hợp lệ.");

        BoxCoxLambdas = doubleLambdas
            .ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
    }

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


