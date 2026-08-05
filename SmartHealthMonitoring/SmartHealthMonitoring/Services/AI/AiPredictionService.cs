using SmartHealthMonitoring.Interfaces.AI;
using ModelsPatient = SmartHealthMonitoring.Models.Patient;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI;

public class AiPredictionService : IAiPredictionService
{
    private readonly IAiModelSessionRunner _sessionRunner;
    private readonly ILogger<AiPredictionService> _logger;

    private IReadOnlyDictionary<string, float> Lambdas => _sessionRunner.BoxCoxLambdas;

    public AiPredictionService(IAiModelSessionRunner sessionRunner, ILogger<AiPredictionService> logger)
    {
        _sessionRunner = sessionRunner;
        _logger = logger;
    }


    private float ApplyBoxCox(float value, string featureName)
    {
        if (!Lambdas.TryGetValue(featureName, out float lambda))
        {
            _logger.LogWarning("[BoxCox] Khong tim thay lambda cho feature '{Feature}'. Dung gia tri goc.", featureName);
            return value;
        }

        if (value <= 0f)
        {
            _logger.LogWarning("[BoxCox] Feature '{Feature}' co gia tri {Value} <= 0. Thay bang epsilon.", featureName, value);
            value = 1e-6f;
        }

        if (Math.Abs(lambda) < 1e-10f)
            return (float)Math.Log(value);

        return (float)((Math.Pow(value, lambda) - 1.0) / lambda);
    }


    private float[] BuildFeatureVector(
        int age, float sex,
        float trestbps, float chol, float fbs,
        float thalach, float exang, float oldpeakRaw,
        float slope, float ca,
        byte cpRaw, byte restecgRaw, byte thalRaw)
    {
        float ageT      = ApplyBoxCox((float)age, "age");
        float trestbpsT = ApplyBoxCox(trestbps,   "trestbps");
        float cholT     = ApplyBoxCox(chol,        "chol");
        float thalachT  = ApplyBoxCox(thalach,     "thalach");
        float oldpeakT  = ApplyBoxCox(oldpeakRaw,  "oldpeak");

        byte uciCp = cpRaw switch
        {
            0 => 3, // Khong dau → Asymptomatic
            1 => 2, // Dau nhe  → Non-anginal pain
            2 => 1, // Dau vua  → Atypical angina
            3 => 0, // Dau nang → Typical angina (nguy hiem nhat)
            _ => 3
        };

        float cp_1 = uciCp == 1 ? 1f : 0f;
        float cp_2 = uciCp == 2 ? 1f : 0f;
        float cp_3 = uciCp == 3 ? 1f : 0f;

        float restecg_1 = restecgRaw == 1 ? 1f : 0f;
        float restecg_2 = restecgRaw == 2 ? 1f : 0f;

        float thal_1 = thalRaw == 1 ? 1f : 0f;
        float thal_2 = thalRaw == 2 ? 1f : 0f;
        float thal_3 = thalRaw == 3 ? 1f : 0f;

        _logger.LogDebug(
            "[Debug] Dau vao truoc Box-Cox: age={Age}, sex={Sex}, BP={Trestbps}, chol={Chol}, fbs={Fbs}, HR={Thalach}, exang={Exang}, oldpeak={Oldpeak}, slope={Slope}, ca={Ca}, cp={Cp}, ecg={Restecg}, thal={Thal}",
            age, sex, trestbps, chol, fbs, thalach, exang, oldpeakRaw,
            slope, ca, cpRaw, restecgRaw, thalRaw);

        _logger.LogDebug(
            "[Debug] Feature vector (sau Box-Cox + One-Hot): age={AgeT:F3}, sex={Sex}, BP={TrestbpsT:F3}, chol={CholT:F3}, fbs={Fbs}, HR={ThalachT:F3}, exang={Exang}, op={OldpeakT:F3}, slope={Slope}, ca={Ca}, cp=[{Cp1},{Cp2},{Cp3}], ecg=[{Rec1},{Rec2}], thal=[{Th1},{Th2},{Th3}]",
            ageT, sex, trestbpsT, cholT, fbs, thalachT, exang, oldpeakT,
            slope, ca,
            cp_1, cp_2, cp_3,
            restecg_1, restecg_2,
            thal_1, thal_2, thal_3);

        return new float[]
        {
            ageT, sex, trestbpsT, cholT, fbs, thalachT,
            exang, oldpeakT, slope, ca,
            cp_1, cp_2, cp_3,
            restecg_1, restecg_2,
            thal_1, thal_2, thal_3
        };
    }


    private AiriskPrediction RunInference(float[] featureValues, string modelType, string dataSource)
    {
        var inputTensor = new DenseTensor<float>(featureValues, new[] { 1, 18 });

        var session = _sessionRunner.GetSession(modelType);
        var inputs  = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
        };

        using var results = session.Run(inputs);

        float prob_disease; // xac suat CO BENH TIM (class=0)
        float prob_healthy; // xac suat KHONG BENH  (class=1)

        var resultList = results.ToList();
        bool isAnfis = modelType.Equals("ANFIS", StringComparison.OrdinalIgnoreCase);

        if (isAnfis)
        {
            // ANFIS (PyTorch): Output[0] = label (int64), Output[1] = probabilities (float tensor)
            try
            {
                var probTensor = resultList.Count > 1
                    ? resultList[1].AsEnumerable<float>().ToArray()
                    : resultList[0].AsEnumerable<float>().ToArray();

                prob_disease = probTensor.Length >= 1 ? probTensor[0] : 0f;
                prob_healthy = probTensor.Length >= 2 ? probTensor[1] : 1f;

                if (prob_disease < 0f || prob_disease > 1f || prob_healthy < 0f || prob_healthy > 1f
                    || Math.Abs(prob_disease + prob_healthy - 1f) > 0.01f)
                {
                    float maxLogit = Math.Max(prob_disease, prob_healthy);
                    float exp0     = (float)Math.Exp(prob_disease - maxLogit);
                    float exp1     = (float)Math.Exp(prob_healthy - maxLogit);
                    float sumExp   = exp0 + exp1;

                    _logger.LogDebug("[ANFIS] Logit goc: class0={L0:F3}, class1={L1:F3} -> ap dung Softmax.", prob_disease, prob_healthy);

                    prob_disease = exp0 / sumExp;
                    prob_healthy = exp1 / sumExp;
                }

                _logger.LogDebug("[ANFIS] Xac suat: CoBenh={P0:F3}, KhongBenh={P1:F3}", prob_disease, prob_healthy);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ANFIS] Loi doc output tensor, dung fallback mac dinh (CoBenh=0).");
                prob_disease = 0f;
                prob_healthy = 1f;
            }
        }
        else
        {
            try
            {
                // zipmap=False → output la float tensor [1, 2]
                var probTensor = resultList.Skip(1).First().AsEnumerable<float>().ToArray();
                prob_disease = probTensor.Length >= 1 ? probTensor[0] : 0f;
                prob_healthy = probTensor.Length >= 2 ? probTensor[1] : 1f;
                _logger.LogDebug("[{Model}] Xac suat: CoBenh={P0:F3}, KhongBenh={P1:F3}", modelType, prob_disease, prob_healthy);
            }
            catch
            {
                var probMaps = resultList.Skip(1).First()
                    .AsEnumerable<IDictionary<long, float>>().ToArray();
                prob_disease = probMaps[0].TryGetValue(0L, out var p0) ? p0 : 0f;
                prob_healthy = probMaps[0].TryGetValue(1L, out var p1) ? p1 : 1f;
                _logger.LogDebug("[{Model}] Xac suat (Map): CoBenh={P0:F3}, KhongBenh={P1:F3}", modelType, prob_disease, prob_healthy);
            }
        }

        float prob1        = prob_disease;
        long  predictedLabel = prob1 >= 0.5f ? 1L : 0L;
        decimal riskScore    = (decimal)prob1;

        byte riskLevel = riskScore >= 0.70m ? (byte)3
                       : riskScore >= 0.40m ? (byte)2
                       : (byte)1;

        return new AiriskPrediction
        {
            PredictedAt     = DateTime.Now,
            RiskScore       = riskScore,
            PredictedTarget = (byte)predictedLabel,
            ModelVersion    = dataSource,
            RiskLevel       = riskLevel
        };
    }


    public AiriskPrediction PredictHeartDiseaseRisk(ClinicalRecord record, string modelType = "KNN")
    {
        try
        {
            if (record.Patient == null)
                throw new ArgumentException("Record phai bao gom thong tin Patient de tinh tuoi.", nameof(record));

            int   age = CalculateAge(record.Patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = record.Patient.Sex;

            var (normalBP, normalMaxHR, normalChol) = GetNormalValues(age, sex);

            float restingBp  = record.RestingBp.HasValue    ? (float)record.RestingBp.Value    : normalBP;
            float chol       = record.Cholesterol.HasValue  ? (float)record.Cholesterol.Value  : normalChol;
            float maxHR      = record.MaxHeartRate.HasValue ? (float)record.MaxHeartRate.Value : normalMaxHR;
            float fbs        = (record.FastingBs.HasValue && (record.FastingBs.Value > 120 || record.FastingBs.Value == 1)) ? 1f : 0f;
            float oldpeak    = record.OldPeak.HasValue      ? (float)record.OldPeak.Value + 0.001f : 0.001f;
            float exang      = record.ExerciseAngina.HasValue ? (float)record.ExerciseAngina.Value : 0f;
            float slope      = record.Stslope.HasValue      ? (float)record.Stslope.Value      : 2f;
            float ca         = record.MajorVessels.HasValue ? (float)record.MajorVessels.Value : 0f;
            byte  cpRaw      = record.ChestPainType   ?? (byte)3; // Asymptomatic if null
            byte  restecgRaw = record.RestEcg         ?? (byte)0; // Normal if null
            byte  thalRaw    = record.ThalResult       ?? (byte)1; // Normal if null

            var featureValues = BuildFeatureVector(
                age:        age,
                sex:        sex,
                trestbps:   restingBp,
                chol:       chol,
                fbs:        fbs,
                thalach:    maxHR,
                exang:      exang,
                oldpeakRaw: oldpeak,
                slope:      slope,
                ca:         ca,
                cpRaw:      cpRaw,
                restecgRaw: restecgRaw,
                thalRaw:    thalRaw
            );

            return RunInference(featureValues, modelType, $"{modelType}_Clinical_1.0");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI] Loi du doan tu ClinicalRecord (Model={Model})", modelType);
            throw;
        }
    }

    public AiriskPrediction PredictCombined(DailyVitalLog log, ClinicalRecord? clinicalRecord, ModelsPatient patient, string modelType = "KNN", IReadOnlyList<string>? purchasedServiceNames = null)
    {
        try
        {
            if (patient == null)
                throw new ArgumentException("ModelsPatient khong duoc null.", nameof(patient));

            int   age = CalculateAge(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = patient.Sex;

            _logger.LogDebug(
                "[PredictCombined] PurchasedServices={Svcs} (Fallback giờ đây dựa vào null-check trực tiếp từ ClinicalRecord)",
                purchasedServiceNames == null ? "ALL" : string.Join(", ", purchasedServiceNames));

            float trestbps = log.SystolicBp;
            float exang    = log.HasExerciseAngina ? 1f : 0f;
            byte  cpRaw    = log.ChestPainLevel;

            float restingHR        = log.HeartRate;
            float theoreticalMaxHR = 220f - age;
            float hrPenalty        = Math.Clamp((restingHR - 75f) / 300f, 0f, 0.5f);
            float thalach          = theoreticalMaxHR * (1f - hrPenalty);

            _logger.LogDebug(
                "[HRConvert] Nhip nghi={RHR}bpm, Tuoi={Age} -> HR max ly thuyet={TMax:F0}, Phat={Pen:P0} -> HR max uoc tinh={Thalach:F0}bpm",
                restingHR, age, theoreticalMaxHR, hrPenalty, thalach);


            var (normalBP, normalMaxHR, normalChol) = GetNormalValues(age, sex);
            _logger.LogDebug(
                "[NormalValues] Age={A}, Sex={S} → BP={BP}, MaxHR={HR}, Chol={C}",
                age, sex, normalBP, normalMaxHR, normalChol);

            float chol;
            float fbs;
            if (clinicalRecord?.Cholesterol != null)
            {
                chol = (float)clinicalRecord.Cholesterol.Value;
                fbs  = (clinicalRecord.FastingBs.HasValue && (clinicalRecord.FastingBs.Value > 120 || clinicalRecord.FastingBs.Value == 1)) ? 1f : 0f;
                _logger.LogDebug("[Blood] Dùng giá trị thực từ ClinicalRecord: chol={C}, fbs={F}", chol, fbs);
            }
            else
            {
                // Cholesterol = null trong DB → gói chưa mua hoặc chưa đo → fallback theo tuổi/giới
                chol = normalChol;
                fbs  = 0f;
                _logger.LogDebug("[Blood] Cholesterol=null → fallback theo tuổi/giới: chol={C}", chol);
            }

            float oldpeak;
            float slope;
            float ca;
            byte  restecgRaw;
            byte  thalRaw;
            if (clinicalRecord?.OldPeak != null)
            {
                oldpeak    = (float)clinicalRecord.OldPeak.Value + 0.001f;
                slope      = clinicalRecord.Stslope.HasValue ? (float)clinicalRecord.Stslope.Value : 2f;
                ca         = clinicalRecord.MajorVessels.HasValue ? (float)clinicalRecord.MajorVessels.Value : 0f;
                restecgRaw = clinicalRecord.RestEcg ?? (byte)0;
                thalRaw    = clinicalRecord.ThalResult ?? (byte)1;
                _logger.LogDebug("[ECG] Dùng giá trị thực từ ClinicalRecord");
            }
            else
            {
                // OldPeak = null trong DB → gói chưa mua hoặc chưa đo → fallback bình thường
                oldpeak    = 0.001f; // ST depression = 0 → không bất thường
                slope      = 2f;     // Upsloping = bình thường nhất
                ca         = 0f;     // 0 động mạch tắc → tốt
                restecgRaw = (byte)0; // Normal ECG
                thalRaw    = (byte)1; // Normal thalassemia
                _logger.LogDebug("[ECG] OldPeak=null → fallback bình thường (oldpeak=0, slope=Up, ca=0)");
            }

            var featureValues = BuildFeatureVector(
                age:        age,
                sex:        sex,
                trestbps:   trestbps,
                chol:       chol,
                fbs:        fbs,
                thalach:    thalach,
                exang:      exang,
                oldpeakRaw: oldpeak,
                slope:      slope,
                ca:         ca,
                cpRaw:      cpRaw,
                restecgRaw: restecgRaw,
                thalRaw:    thalRaw
            );

            string dataSource = clinicalRecord != null
                ? $"DL+Clin_{modelType}_1.0"
                : $"DailyLog_{modelType}_1.0";

            return RunInference(featureValues, modelType, dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI] Loi du doan ket hop (Model={Model})", modelType);
            throw;
        }
    }


    private static (float RestingBP, float MaxHR, float Cholesterol) GetNormalValues(int age, float sex)
    {
        float restingBP = sex >= 1f // 1 = Nam
            ? age switch
            {
                < 30 => 115f,
                < 40 => 120f,
                < 50 => 124f,
                < 60 => 128f,
                < 70 => 132f,
                _    => 136f
            }
            : age switch // Nu
            {
                < 30 => 110f,
                < 40 => 114f,
                < 50 => 118f,
                < 60 => 128f, // Tăng mạnh sau mãn kinh
                < 70 => 134f,
                _    => 138f
            };

        float maxHR = (220f - age) + (sex >= 1f ? 0f : 5f);
        maxHR = Math.Clamp(maxHR, 100f, 200f);

        float chol = sex >= 1f // Nam
            ? age switch
            {
                < 30 => 180f,
                < 40 => 195f,
                < 50 => 210f,
                < 60 => 220f,
                < 70 => 215f,
                _    => 210f
            }
            : age switch // Nu
            {
                < 30 => 170f,
                < 40 => 185f,
                < 50 => 200f,
                < 60 => 230f, // Post-menopause jump
                < 70 => 240f,
                _    => 235f
            };

        return (restingBP, maxHR, chol);
    }


    private static int CalculateAge(DateTime dateOfBirth)
    {
        int age = DateTime.Now.Year - dateOfBirth.Year;
        if (DateTime.Now.DayOfYear < dateOfBirth.DayOfYear)
            age--;
        return age;
    }
}


