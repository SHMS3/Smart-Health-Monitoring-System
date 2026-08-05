using ModelsPatient = SmartHealthMonitoring.Models.Patient;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.AI;

public interface IAiPredictionService
{
    AiriskPrediction PredictHeartDiseaseRisk(ClinicalRecord record, string modelType = "SVM");

    AiriskPrediction PredictCombined(DailyVitalLog log, ClinicalRecord? clinicalRecord, ModelsPatient patient, string modelType = "SVM", IReadOnlyList<string>? purchasedServiceNames = null);
}


