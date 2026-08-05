using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.AI;

public class ExplainabilityResult
{
    public string Summary { get; set; } = string.Empty;

    public List<string> RiskFactors { get; set; } = new();

    public List<string> Protective { get; set; } = new();

    public string DataSource { get; set; } = string.Empty;
}

public interface IAnfisExplainabilityService
{
    ExplainabilityResult Explain(AiriskPrediction prediction, WarningAlert alert);
}
