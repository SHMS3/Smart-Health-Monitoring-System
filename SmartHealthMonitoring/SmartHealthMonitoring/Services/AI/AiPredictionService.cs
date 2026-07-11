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

            // Fallback theo tuổi/giới cho các field null (gói chưa mua)
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

    /// <summary>
    /// Du doan ket hop: uu tien chi so sinh hieu moi nhat tu DailyVitalLog,
    /// bo sung bang chi so xet nghiem tu ClinicalRecord gan nhat (co the null).
    ///
    /// DailyVitalLog cung cap:  SystolicBp → trestbps, HeartRate → thalach (uoc tinh),
    ///                          ChestPainLevel → cp, HasExerciseAngina → exang
    /// ClinicalRecord cung cap: Cholesterol, FastingBS, RestECG, OldPeak, STSlope, MajorVessels, ThalResult
    /// Neu ClinicalRecord null hoac da cu → dung gia tri trung binh lam sang lam fallback.
    ///
    /// purchasedServiceNames: Danh sach ten dich vu da thanh toan (lowercase).
    ///   - "huyết áp & triệu chứng" → nhom cp/exang/bp
    ///   - "phân tích huyết học"    → nhom chol/fbs
    ///   - "điện tâm đồ & mạch vành" → nhom ecg/oldpeak/slope/ca/thal
    /// Neu null hoac empty → dung fallback an toan cho tat ca.
    /// </summary>
    public AiriskPrediction PredictCombined(DailyVitalLog log, ClinicalRecord? clinicalRecord, Patient patient, string modelType = "KNN", IReadOnlyList<string>? purchasedServiceNames = null)
    {
        try
        {
            if (patient == null)
                throw new ArgumentException("Patient khong duoc null.", nameof(patient));

            int   age = CalculateAge(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue));
            float sex = patient.Sex;

            // ── Ghi log các gói dịch vụ đã mua (chỉ để debug, không dùng để quyết định fallback nữa) ──
            _logger.LogDebug(
                "[PredictCombined] PurchasedServices={Svcs} (Fallback giờ đây dựa vào null-check trực tiếp từ ClinicalRecord)",
                purchasedServiceNames == null ? "ALL" : string.Join(", ", purchasedServiceNames));

            // ── Chi so tu DailyVitalLog (luon dung - du lieu moi nhat) ──────────
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

            // ── Chi so tu ClinicalRecord ─────────────────────────────────────────
            // Fallback thong minh theo GOI DICH VU DA MUA:
            //   - Goi chua mua → dung gia tri trung binh nguoi khoe manh (UCI mean)
            //   - Dam bao mo hinh ONNX luon nhan du 18 features hop le

            // Nhom Huyết học: Cholesterol, FastingBS
            // Fallback: kiểm tra null trực tiếp từ ClinicalRecord (không còn dùng hasBloodPackage nữa)
            var (normalBP, normalMaxHR, normalChol) = GetNormalValues(age, sex);
            _logger.LogDebug(
                "[NormalValues] Age={A}, Sex={S} → BP={BP}, MaxHR={HR}, Chol={C}",
                age, sex, normalBP, normalMaxHR, normalChol);

            float chol;
            float fbs;
            if (clinicalRecord?.Cholesterol != null)
            {
                // Có dữ liệu thực tế trong DB → dùng luôn
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

            // Nhom Điện tâm đồ & Mạch vành: OldPeak, STSlope, RestECG, MajorVessels, ThalResult
            // Fallback: kiểm tra null trực tiếp từ ClinicalRecord
            float oldpeak;
            float slope;
            float ca;
            byte  restecgRaw;
            byte  thalRaw;
            if (clinicalRecord?.OldPeak != null)
            {
                // Có dữ liệu ECG thực tế → dùng
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


    // =========================================================================
    // Clinical Normal Values by Age & Sex
    // =========================================================================

    /// <summary>
    /// Tra ve gia tri sinh ly BINH THUONG cho 1 benh nhan cu the dua tren tuoi va gioi tinh.
    /// Tham chieu:
    ///   - Huyết ap: ACC/AHA 2017 guideline
    ///   - Cholesterol: NCEP-ATP III / AHA by age-sex strata
    ///   - Nhip tim toi da: Haskell-Fox (220-age), hieu chinh gioi (nu cao hon 5-7 bpm)
    ///   - Oldpeak, STSlope, ca, ECG, Thal: khong thay doi theo tuoi (gia tri categorical on dinh)
    /// </summary>
    private static (float RestingBP, float MaxHR, float Cholesterol) GetNormalValues(int age, float sex)
    {
        // ── Huyết áp tâm thu nghỉ (mmHg) ────────────────────────────────────────
        // Nam: thap hon nu truoc 50t, tu 50t tro len tuong duong
        // Nu:  tang nhanh sau man kinh (~50t)
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

        // ── Nhịp tim tối đa khi gắng sức (bpm) ─────────────────────────────────
        // Haskell-Fox: 220 - age (cho nam)
        // Hiệu chỉnh nữ: +5 bpm (trung bình nữ đạt HR tối đa cao hơn)
        float maxHR = (220f - age) + (sex >= 1f ? 0f : 5f);
        // Clamp về khoảng sinh lý (100-200 bpm)
        maxHR = Math.Clamp(maxHR, 100f, 200f);

        // ── Cholesterol toàn phần (mg/dL) ───────────────────────────────────────
        // NCEP-ATP III / AHA normals theo lứa tuổi và giới:
        //   Nam: tăng dần đến 50t rồi ổn định
        //   Nữ: tăng mạnh sau mãn kinh (50t), đỉnh ở 60-70t
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
