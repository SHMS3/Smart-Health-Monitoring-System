using Microsoft.ML.OnnxRuntime;

namespace SmartHealthMonitoring.Interfaces.AI;

public interface IAiModelSessionRunner : IDisposable
{
    InferenceSession GetSession(string modelType);

    IReadOnlyDictionary<string, float> BoxCoxLambdas { get; }
}
