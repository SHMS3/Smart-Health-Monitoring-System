using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI;

/// <summary>
/// Scoped Service trien khai logic chuan bi du lieu (mapping + Box-Cox transform) va goi ONNX inference.
/// </summary>
public class AiPredictionService : IAiPredictionService
{
    private readonly IAiModelSessionRunner _sessionRunner;
    private readonly ILogger<AiPredictionService> _logger;

    // Lay lambdas tu Singleton SessionRunner (da nap 1 lan luc startup)
    private IReadOnlyDictionary<string, float> Lambdas => _sessionRunner.BoxCoxLambdas;

    public AiPredictionService(IAiModelSessionRunner sessionRunner, ILogger<AiPredictionService> logger)
    {
        _sessionRunner = sessionRunner;
        _logger = logger;
    }

    // =========================================================================
    // Box-Cox Transform
    // =========================================================================

    /// <summary>
    /// Ap dung bien doi Box-Cox len mot gia tri continuous.
    /// Neu lambda ≈ 0 → dung ln(x). Nguoc lai → (x^λ − 1) / λ.
    /// Neu khong tim thay lambda → tra ve gia tri goc (fallback an toan).
    /// </summary>
    private float ApplyBoxCox(float value, string featureName)
    {
        if (!Lambdas.TryGetValue(featureName, out float lambda))
        {
            _logger.LogWarning("[BoxCox] Khong tim thay lambda cho feature '{Feature}'. Dung gia tri goc.", featureName);
            return value;
        }

        // Bao ve khoi log(0) hoac pow am
        if (value <= 0f)
        {
            _logger.LogWarning("[BoxCox] Feature '{Feature}' co gia tri {Value} <= 0. Thay bang epsilon.", featureName, value);
            value = 1e-6f;
        }

        // lambda ≈ 0 → log transform
        if (Math.Abs(lambda) < 1e-10f)
            return (float)Math.Log(value);

        return (float)((Math.Pow(value, lambda) - 1.0) / lambda);
    }

    // =========================================================================
    // Feature Vector Builder (dung chung cho ca 2 predict methods)
    // =========================================================================

    /// <summary>
    /// Tao feature vector 18 chieu dung thu tu huan luyen:
    /// [0] age, [1] sex, [2] trestbps, [3] chol, [4] fbs,
    /// [5] thalach, [6] exang, [7] oldpeak, [8] slope, [9] ca,
    /// [10-12] cp_1/2/3 (One-Hot), [13-14] restecg_1/2 (One-Hot),
    /// [15-17] thal_1/2/3 (One-Hot)
    /// </summary>
    private float[] BuildFeatureVector(
        int age, float sex,
        float trestbps, float chol, float fbs,
        float thalach, float exang, float oldpeakRaw,
        float slope, float ca,
        byte cpRaw, byte restecgRaw, byte thalRaw)
    {
        // Box-Cox tren continuous features
        float ageT      = ApplyBoxCox((float)age, "age");
        float trestbpsT = ApplyBoxCox(trestbps,   "trestbps");
        float cholT     = ApplyBoxCox(chol,        "chol");
        float thalachT  = ApplyBoxCox(thalach,     "thalach");
        // oldpeak da duoc cong +0.001 tu caller de tranh log(0)
        float oldpeakT  = ApplyBoxCox(oldpeakRaw,  "oldpeak");

        // One-Hot Encoding cho Chest Pain
        // QUAN TRONG - MAPPING NGUOC:
        // App: 0=Khong dau, 1=Nhe, 2=Vua, 3=Nang
        // UCI: 0=Typical Angina (NGUY HIEM NHAT), 1=Atypical, 2=Non-anginal, 3=Asymptomatic (it nguy hiem nhat)
        // => Dao mapping: app=0 → uci=3, app=3 → uci=0
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

        // DEBUG: Chi hien o LogLevel=Debug
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

    // =========================================================================
    // Inference Helper
    // =========================================================================

    private AiriskPrediction RunInference(float[] featureValues, string modelType, string dataSource)
    {
        // Tao Tensor 2D [1 x 18]
        var inputTensor = new DenseTensor<float>(featureValues, new[] { 1, 18 });

        var session = _sessionRunner.GetSession(modelType);
        var inputs  = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
        };

        using var results = session.Run(inputs);

        // QUAN TRONG - NHAN DATASET UCI:
        //   class 0 = CO BENH TIM (ton thuong dong mach vanh)
        //   class 1 = KHONG BENH (binh thuong)
        // => riskScore = probabilities[0] (xac suat class=0 = xac suat CO BENH)
        // ANFIS cung dung quy uoc nhan giong het KNN/SVM (train cung dataset UCI)
        float prob_disease; // xac suat CO BENH TIM (class=0)
        float prob_healthy; // xac suat KHONG BENH  (class=1)

        var resultList = results.ToList();
        bool isAnfis = modelType.Equals("ANFIS", StringComparison.OrdinalIgnoreCase);

        if (isAnfis)
        {
            // ANFIS (PyTorch): Output[0] = label (int64), Output[1] = probabilities (float tensor)
            // PyTorch model xuat float tensor truc tiep (KHONG phai Sequence<Map>)
            try
            {
                var probTensor = resultList.Count > 1
                    ? resultList[1].AsEnumerable<float>().ToArray()
                    : resultList[0].AsEnumerable<float>().ToArray();

                prob_disease = probTensor.Length >= 1 ? probTensor[0] : 0f;
                prob_healthy = probTensor.Length >= 2 ? probTensor[1] : 1f;

                // ANFIS thuong tra ve Logits thay vi Probabilities.
                // Ap dung Softmax neu tong khong xap xi 1 hoac co gia tri ngoai [0,1]
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
            // KNN/SVM (scikit-learn): Output[0] = label, Output[1] = probabilities
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
                // Fallback: Sequence<Map<int64, float>>
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

        // RiskLevel: 1=Thap (<0.40), 2=Trung binh (0.40-0.70), 3=Cao (>=0.70)
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

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Du doan nguy co tim mach dua tren ClinicalRecord (kham tai vien).
    /// Dung khi benh nhan KHONG co DailyVitalLog nao.
    /// </summary>
    public AiriskPrediction PredictHeartDiseaseRisk(ClinicalRecord record, string modelType = "KNN")
    {
        try
        {
            if (record.Patient == null)
                throw new ArgumentException("Record phai bao gom thong tin Patient de tinh tuoi.", nameof(record));

            int   age = CalculateAge(record.Patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = record.Patient.Sex;

            float fbs     = (record.FastingBs > 120 || record.FastingBs == 1) ? 1f : 0f;
            float oldpeak = (float)record.OldPeak + 0.001f; // +0.001 de tranh log(0)

            var featureValues = BuildFeatureVector(
                age:        age,
                sex:        sex,
                trestbps:   record.RestingBp,
                chol:       record.Cholesterol,
                fbs:        fbs,
                thalach:    record.MaxHeartRate,
                exang:      record.ExerciseAngina,
                oldpeakRaw: oldpeak,
                slope:      record.Stslope,
                ca:         record.MajorVessels,
                cpRaw:      record.ChestPainType,
                restecgRaw: record.RestEcg,
                thalRaw:    record.ThalResult
            );

            return RunInference(featureValues, modelType, $"{modelType}_Clinical_1.0");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI] Loi du doan tu ClinicalRecord (Model={Model})", modelType);
            throw;
        }
    }

    /// <summary>
    /// Du doan ket hop: uu tien chi so sinh hieu moi nhat tu DailyVitalLog,
    /// bo sung bang chi so xet nghiem tu ClinicalRecord gan nhat (co the null).
    ///
    /// DailyVitalLog cung cap:  SystolicBp → trestbps, HeartRate → thalach (uoc tinh),
    ///                          ChestPainLevel → cp, HasExerciseAngina → exang
    /// ClinicalRecord cung cap: Cholesterol, FastingBS, RestECG, OldPeak, STSlope, MajorVessels, ThalResult
    /// Neu ClinicalRecord null hoac da cu → dung gia tri trung binh lam sang lam fallback.
    /// </summary>
    public AiriskPrediction PredictCombined(DailyVitalLog log, ClinicalRecord? clinicalRecord, Patient patient, string modelType = "KNN")
    {
        try
        {
            if (patient == null)
                throw new ArgumentException("Patient khong duoc null.", nameof(patient));

            int   age = CalculateAge(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = patient.Sex;

            // Chi so tu DailyVitalLog (luon dung - du lieu moi nhat)
            float trestbps = log.SystolicBp;
            float exang    = log.HasExerciseAngina ? 1f : 0f;
            byte  cpRaw    = log.ChestPainLevel;

            // MAPPING NHIP TIM:
            // DailyVitalLog.HeartRate = nhip tim NGHI NGOI tai nha (resting HR)
            // UCI thalach = nhip tim TOI DA khi GANG SUC (max exercise HR)
            //
            // Trong UCI: thalach CAO → KHOE MANH | thalach THAP → CO BENH
            // Nhip nghi cao (>=100 bpm = Tachycardia) → tim kem cardiac reserve → max HR thap khi gang suc
            //
            // Cong thuc uoc tinh: theoreticalMax = 220 - age (Haskell-Fox)
            // Penalty tang dan tu 0% (HR=75) toi 50% (HR=375)
            float restingHR        = log.HeartRate;
            float theoreticalMaxHR = 220f - age;
            float hrPenalty        = Math.Clamp((restingHR - 75f) / 300f, 0f, 0.5f);
            float thalach          = theoreticalMaxHR * (1f - hrPenalty);

            _logger.LogDebug(
                "[HRConvert] Nhip nghi={RHR}bpm, Tuoi={Age} -> HR max ly thuyet={TMax:F0}, Phat={Pen:P0} -> HR max uoc tinh={Thalach:F0}bpm",
                restingHR, age, theoreticalMaxHR, hrPenalty, thalach);

            // Chi so tu ClinicalRecord gan nhat (neu co, khong qua 90 ngay)
            // Fallback = gia tri trung binh lam sang tu UCI Heart Disease Dataset
            float chol = clinicalRecord != null ? (float)clinicalRecord.Cholesterol : 246f;
            float fbs  = clinicalRecord != null
                             ? ((clinicalRecord.FastingBs > 120 || clinicalRecord.FastingBs == 1) ? 1f : 0f)
                             : 0f;
            float oldpeak = clinicalRecord != null
                                ? (float)clinicalRecord.OldPeak + 0.001f
                                : 0.001f; // Fallback = 0 (khong co ST depression = binh thuong)
            float slope   = clinicalRecord != null ? (float)clinicalRecord.Stslope      : 1f; // 1=Flat
            float ca      = clinicalRecord != null ? (float)clinicalRecord.MajorVessels : 0f;

            byte restecgRaw = clinicalRecord != null ? clinicalRecord.RestEcg    : (byte)1; // 1=Normal
            byte thalRaw    = clinicalRecord != null ? clinicalRecord.ThalResult  : (byte)1; // 1=Normal

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

    // =========================================================================
    // Helpers
    // =========================================================================

    private static int CalculateAge(DateTime dateOfBirth)
    {
        int age = DateTime.Now.Year - dateOfBirth.Year;
        if (DateTime.Now.DayOfYear < dateOfBirth.DayOfYear)
            age--;
        return age;
    }
}
