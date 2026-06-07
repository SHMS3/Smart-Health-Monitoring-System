using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services;

/// <summary>
/// Scoped Service triển khai logic chuẩn bị dữ liệu (mapping + Box-Cox transform) và gọi ONNX inference.
/// </summary>
public class AiPredictionService : IAiPredictionService
{
    private readonly IAiModelSessionRunner _sessionRunner;
    private readonly ILogger<AiPredictionService> _logger;

    // Lấy lambdas từ Singleton SessionRunner (đã nạp 1 lần lúc startup)
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
    /// Áp dụng phép biến đổi Box-Cox lên một giá trị continuous.
    /// Nếu lambda ≈ 0 → dùng ln(x). Ngược lại → (x^λ − 1) / λ.
    /// Nếu không tìm thấy lambda trong từ điển → trả về giá trị gốc (fallback an toàn).
    /// </summary>
    /// <param name="value">Giá trị đầu vào (phải > 0)</param>
    /// <param name="featureName">Tên feature trong boxcox_lambdas.json</param>
    private float ApplyBoxCox(float value, string featureName)
    {
        if (!Lambdas.TryGetValue(featureName, out float lambda))
        {
            _logger.LogWarning("Box-Cox lambda không tìm thấy cho feature '{Feature}'. Sử dụng giá trị gốc.", featureName);
            return value;
        }

        // Bảo vệ khỏi log(0) hoặc pow âm
        if (value <= 0f)
        {
            _logger.LogWarning("Giá trị feature '{Feature}' = {Value} <= 0. Gán thành epsilon trước khi Box-Cox.", featureName, value);
            value = 1e-6f;
        }

        // lambda ≈ 0 → log transform
        if (Math.Abs(lambda) < 1e-10f)
            return (float)Math.Log(value);

        return (float)((Math.Pow(value, lambda) - 1.0) / lambda);
    }

    // =========================================================================
    // Feature Vector Builder (dùng chung cho cả 2 predict methods)
    // =========================================================================

    /// <summary>
    /// Tạo feature vector 18 chiều đúng thứ tự huấn luyện:
    /// [0]  age         (Box-Cox)
    /// [1]  sex         (binary: 1=Nam, 0=Nữ)
    /// [2]  trestbps    (Box-Cox)
    /// [3]  chol        (Box-Cox)
    /// [4]  fbs         (binary: 1 nếu đường huyết > 120 mg/dl)
    /// [5]  thalach     (Box-Cox)
    /// [6]  exang       (binary: 1=có đau ngực khi vận động)
    /// [7]  oldpeak     (Box-Cox, đã cộng +0.001 để tránh log(0))
    /// [8]  slope       (ordinal: 0/1/2)
    /// [9]  ca          (ordinal: 0–3)
    /// [10] cp_1        (One-Hot: ChestPainType == 1)
    /// [11] cp_2        (One-Hot: ChestPainType == 2)
    /// [12] cp_3        (One-Hot: ChestPainType == 3)
    /// [13] restecg_1   (One-Hot: RestEcg == 1)
    /// [14] restecg_2   (One-Hot: RestEcg == 2)
    /// [15] thal_1      (One-Hot: ThalResult == 1)
    /// [16] thal_2      (One-Hot: ThalResult == 2)
    /// [17] thal_3      (One-Hot: ThalResult == 3)
    /// </summary>
    private float[] BuildFeatureVector(
        int age, float sex,
        float trestbps, float chol, float fbs,
        float thalach, float exang, float oldpeakRaw,
        float slope, float ca,
        byte cpRaw, byte restecgRaw, byte thalRaw)
    {
        // ── Box-Cox trên continuous features ───────────────────────────────────
        float ageT      = ApplyBoxCox((float)age,   "age");
        float trestbpsT = ApplyBoxCox(trestbps,     "trestbps");
        float cholT     = ApplyBoxCox(chol,         "chol");
        float thalachT  = ApplyBoxCox(thalach,      "thalach");
        // oldpeak đã được cộng +0.001 từ caller để tránh log(0)
        float oldpeakT  = ApplyBoxCox(oldpeakRaw,   "oldpeak");

        // ── One-Hot Encoding ───────────────────────────────────────────────────
        // ── One-Hot Encoding ───────────────────────────────────────────────────
        float cp_1 = cpRaw == 1 ? 1f : 0f;
        float cp_2 = cpRaw == 2 ? 1f : 0f;
        float cp_3 = cpRaw == 3 ? 1f : 0f;

        float restecg_1 = restecgRaw == 1 ? 1f : 0f;
        float restecg_2 = restecgRaw == 2 ? 1f : 0f;

        float thal_1 = thalRaw == 1 ? 1f : 0f;
        float thal_2 = thalRaw == 2 ? 1f : 0f;
        float thal_3 = thalRaw == 3 ? 1f : 0f;

        // ── DEBUG: in toàn bộ giá trị để chẩn đoán ────────────────────────────
        _logger.LogWarning(
            "[DEBUG] RAW INPUT (truoc Box-Cox):\n" +
            "  age={Age}, sex={Sex}, trestbps={Trestbps}, chol={Chol}\n" +
            "  fbs={Fbs}, thalach={Thalach}, exang={Exang}, oldpeak(+0.001)={Oldpeak}\n" +
            "  slope={Slope}, ca={Ca}, cp={Cp}, restecg={Restecg}, thal={Thal}",
            age, sex, trestbps, chol, fbs, thalach, exang, oldpeakRaw,
            slope, ca, cpRaw, restecgRaw, thalRaw);

        _logger.LogWarning(
            "[DEBUG] FEATURE VECTOR (sau Box-Cox + One-Hot):\n" +
            "  [0]age_T={AgeT:F4}  [1]sex={Sex}  [2]trestbps_T={TrestbpsT:F4}  [3]chol_T={CholT:F4}\n" +
            "  [4]fbs={Fbs}  [5]thalach_T={ThalachT:F4}  [6]exang={Exang}  [7]oldpeak_T={OldpeakT:F4}\n" +
            "  [8]slope={Slope}  [9]ca={Ca}\n" +
            "  [10]cp_1={Cp1}  [11]cp_2={Cp2}  [12]cp_3={Cp3}\n" +
            "  [13]restecg_1={Rec1}  [14]restecg_2={Rec2}\n" +
            "  [15]thal_1={Th1}  [16]thal_2={Th2}  [17]thal_3={Th3}",
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
        // Tạo Tensor 2D [1 × 18]
        var inputTensor = new DenseTensor<float>(featureValues, new[] { 1, 18 });

        var session = _sessionRunner.GetSession(modelType);
        var inputs  = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
        };

        using var results = session.Run(inputs);

        // ═══════════════════════════════════════════════════════════════════════
        // QUAN TRỌNG - NHÃN DATASET:
        // Dataset UCI Kaggle này dùng quy ước NGƯỢC:
        //   class 0 = CÓ BỆNH TIM (có tổn thương động mạch vành)
        //   class 1 = KHÔNG BỆNH (bình thường)
        // Bằng chứng: Cell 48 notebook, bệnh nhân target=1 (khỏe mạnh theo dataset),
        // SVM trả về label=0 với 87.99% → prob[0] là xác suất "khỏe mạnh trong dataset"
        // Nhưng theo ngữ nghĩa y tế thực tế (và UI hiển thị):
        //   target=1 trong dataset = bình thường = RiskScore THẤP
        //   target=0 trong dataset = bệnh tim     = RiskScore CAO
        //
        // Cách đọc đúng: riskScore = probabilities[0] (xác suất class=0 = xác suất CÓ BỆNH)
        // ═══════════════════════════════════════════════════════════════════════
        float prob_disease; // xác suất CÓ BỆNH TIM (class=0 trong dataset này)
        float prob_healthy; // xác suất KHÔNG BỆNH  (class=1 trong dataset này)
        try
        {
            // zipmap=False → output là float tensor [1, 2]
            var probTensor = results.Skip(1).First().AsEnumerable<float>().ToArray();
            prob_disease = probTensor.Length >= 1 ? probTensor[0] : 0f; // class=0 = CÓ BỆNH
            prob_healthy = probTensor.Length >= 2 ? probTensor[1] : 1f; // class=1 = KHÔNG BỆNH
            _logger.LogWarning("[DEBUG] Output float[]: prob_class0(CoBenh)={P0:F4}, prob_class1(KhongBenh)={P1:F4}",
                prob_disease, prob_healthy);
        }
        catch
        {
            // Fallback: Sequence<Map<int64, float>>
            var probMaps = results.Skip(1).First()
                .AsEnumerable<IDictionary<long, float>>().ToArray();
            prob_disease = probMaps[0].TryGetValue(0L, out var p0) ? p0 : 0f;
            prob_healthy = probMaps[0].TryGetValue(1L, out var p1) ? p1 : 1f;
            _logger.LogWarning("[DEBUG] Output Map: prob_class0(CoBenh)={P0:F4}, prob_class1(KhongBenh)={P1:F4}",
                prob_disease, prob_healthy);
        }

        // RiskScore = xác suất CÓ BỆNH (class=0 trong UCI dataset này)
        float prob1 = prob_disease;


        long  predictedLabel = prob1 >= 0.5f ? 1L : 0L;
        decimal riskScore    = (decimal)prob1;

        // RiskLevel: 1=Low (<0.40), 2=Medium (0.40–0.70), 3=High/Critical (≥0.70)
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
    /// Dự đoán nguy cơ tim mạch dựa trên ClinicalRecord (khám tại viện).
    /// Dùng khi bệnh nhân KHÔNG có DailyVitalLog nào.
    /// </summary>
    public AiriskPrediction PredictHeartDiseaseRisk(ClinicalRecord record, string modelType = "KNN")
    {
        try
        {
            if (record.Patient == null)
                throw new ArgumentException("Record phải bao gồm thông tin Patient để tính tuổi.", nameof(record));

            int age = CalculateAge(record.Patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = record.Patient.Sex;

            float fbs      = (record.FastingBs > 120 || record.FastingBs == 1) ? 1f : 0f;
            float oldpeak  = (float)record.OldPeak + 0.001f; // +0.001 để tránh log(0)

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
            _logger.LogError(ex, "Lỗi khi dự đoán từ ClinicalRecord (Model={Model})", modelType);
            throw;
        }
    }

    /// <summary>
    /// Dự đoán kết hợp: ưu tiên chỉ số sinh hiệu mới nhất từ DailyVitalLog,
    /// bổ sung bằng chỉ số xét nghiệm từ ClinicalRecord gần nhất (có thể null).
    ///
    /// DailyVitalLog cung cấp:  SystolicBp → trestbps, HeartRate → thalach,
    ///                          ChestPainLevel → cp, HasExerciseAngina → exang
    /// ClinicalRecord cung cấp: Cholesterol, FastingBS, RestECG, OldPeak, STSlope, MajorVessels, ThalResult
    /// Nếu ClinicalRecord null hoặc đã cũ → dùng giá trị trung bình lâm sàng làm fallback.
    /// </summary>
    public AiriskPrediction PredictCombined(DailyVitalLog log, ClinicalRecord? clinicalRecord, Patient patient, string modelType = "KNN")
    {
        try
        {
            if (patient == null)
                throw new ArgumentException("Patient không được null.", nameof(patient));

            int   age = CalculateAge(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = patient.Sex;

            // ── Chỉ số từ DailyVitalLog (luôn dùng — dữ liệu mới nhất) ────────
            float trestbps = log.SystolicBp;
            float thalach  = log.HeartRate;
            float exang    = log.HasExerciseAngina ? 1f : 0f;
            byte  cpRaw    = log.ChestPainLevel;

            // ── Chỉ số từ ClinicalRecord gần nhất (nếu có, không quá 90 ngày) ─
            // Fallback = giá trị trung bình lâm sàng từ UCI Heart Disease Dataset
            float chol      = clinicalRecord != null ? (float)clinicalRecord.Cholesterol : 246f;
            float fbs       = clinicalRecord != null
                                  ? ((clinicalRecord.FastingBs > 120 || clinicalRecord.FastingBs == 1) ? 1f : 0f)
                                  : 0f;
            float oldpeak   = clinicalRecord != null
                                  ? (float)clinicalRecord.OldPeak + 0.001f
                                  : 1.04f + 0.001f; // mean oldpeak ≈ 1.04 trong dataset
            float slope     = clinicalRecord != null ? (float)clinicalRecord.Stslope     : 1f;  // 1=Flat (phổ biến nhất)
            float ca        = clinicalRecord != null ? (float)clinicalRecord.MajorVessels : 0f;

            byte restecgRaw = clinicalRecord != null ? clinicalRecord.RestEcg    : (byte)1; // 1=Normal (phổ biến nhất)
            byte thalRaw    = clinicalRecord != null ? clinicalRecord.ThalResult  : (byte)2; // 2=Fixed defect (mode)

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
            _logger.LogError(ex, "Lỗi khi dự đoán kết hợp (Model={Model})", modelType);
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
