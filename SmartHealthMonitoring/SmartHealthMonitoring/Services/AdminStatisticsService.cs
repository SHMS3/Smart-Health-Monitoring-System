using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services
{
    public class AdminStatisticsService : IAdminStatisticsService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AdminStatisticsService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatisticsViewModel> GetDashboardStatisticsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // 1. Demographics Data
            var patients = await _context.Patients.Where(p => !p.IsDeleted).ToListAsync();

            var ageGroups = new Dictionary<string, int>
            {
                { "Dưới 30", 0 },
                { "31 - 40", 0 },
                { "41 - 50", 0 },
                { "51 - 60", 0 },
                { "Trên 60", 0 }
            };

            var sexGroups = new Dictionary<string, int>
            {
                { "Nữ", 0 },
                { "Nam", 0 }
            };

            foreach (var p in patients)
            {
                int age = today.Year - p.DateOfBirth.Year;
                if (p.DateOfBirth > today.AddYears(-age)) age--;

                if (age < 30) ageGroups["Dưới 30"]++;
                else if (age <= 40) ageGroups["31 - 40"]++;
                else if (age <= 50) ageGroups["41 - 50"]++;
                else if (age <= 60) ageGroups["51 - 60"]++;
                else ageGroups["Trên 60"]++;

                if (p.Sex == 0) sexGroups["Nữ"]++;
                else if (p.Sex == 1) sexGroups["Nam"]++;
            }

            var demographics = new PatientDemographicStatsViewModel
            {
                AgeLabels = ageGroups.Keys.ToList(),
                AgeValues = ageGroups.Values.ToList(),
                SexLabels = sexGroups.Keys.ToList(),
                SexValues = sexGroups.Values.ToList()
            };

            // 2. Clinical Symptoms Data
            var records = await _context.ClinicalRecords.Where(c => !c.IsDeleted).ToListAsync();

            var cpGroups = records.GroupBy(r => r.ChestPainType)
                                  .Select(g => new { Type = g.Key, Count = g.Count() })
                                  .ToList();

            var cpDictionary = new Dictionary<string, int>();
            foreach (var item in cpGroups)
            {
                string label = item.Type switch
                {
                    0 => "Asymptomatic (ASY)",
                    1 => "Atypical Angina (ATA)",
                    2 => "Non-Anginal Pain (NAP)",
                    3 => "Typical Angina (TA)",
                    _ => $"Khác ({item.Type})"
                };
                cpDictionary[label] = item.Count;
            }

            // Average Cholesterol for age 40-50
            var patients40to50Ids = patients.Where(p =>
            {
                int age = today.Year - p.DateOfBirth.Year;
                if (p.DateOfBirth > today.AddYears(-age)) age--;
                return age >= 40 && age <= 50;
            }).Select(p => p.Id).ToList();

            var records40to50 = records.Where(r => patients40to50Ids.Contains(r.PatientId)).ToList();
            double avgCholesterol = records40to50.Any() ? records40to50.Where(r => r.Cholesterol.HasValue).Average(r => (double)r.Cholesterol!.Value) : 0;

            // FastingBs > 120mg/dl rate
            double highFastingBsRate = 0;
            if (records.Any())
            {
                int highCount = records.Count(r => r.FastingBs.HasValue && r.FastingBs.Value == 1);
                highFastingBsRate = (double)highCount / records.Count * 100;
            }

            var symptoms = new ClinicalSymptomsStatsViewModel
            {
                ChestPainLabels = cpDictionary.Keys.ToList(),
                ChestPainValues = cpDictionary.Values.ToList(),
                AverageCholesterolAge40To50 = Math.Round(avgCholesterol, 2),
                FastingBsHighRate = Math.Round(highFastingBsRate, 2)
            };

            return new DashboardStatisticsViewModel
            {
                Demographics = demographics,
                Symptoms = symptoms
            };
        }

        public async Task<HabitStatisticsViewModel> GetHabitStatisticsAsync()
        {
            var totalPatients = await _context.Patients.CountAsync(p => !p.IsDeleted);
            var habits = await _context.PatientHabits.ToListAsync();
            int total = habits.Count;

            // Metadata: (Key, Label, Description, Category, Type, Icon, Selector)
            var habitMeta = new List<(
                string Key, string Label, string Desc,
                string Category, string Type, string Icon,
                Func<SmartHealthMonitoring.Models.PatientHabit, bool> Selector)>
            {
                // Nhóm 1: Ăn uống
                ("DietSalty",          "Ăn mặn",                    "Lượng muối cao làm tăng huyết áp và tim mạch.",         "Ăn uống",  "bad", "fas fa-pepper-hot",          h => h.DietSalty),
                ("DietHighFat",        "Ăn nhiều chất béo",         "Nguy cơ tắc nghẽn mạch máu, xơ vữa động mạch.",        "Ăn uống",  "bad", "fas fa-drumstick-bite",      h => h.DietHighFat),
                ("DietHighSugar",      "Ăn nhiều đường",            "Tăng nguy cơ tiểu đường tuýp 2, béo phì.",             "Ăn uống",  "bad", "fas fa-cookie-bite",         h => h.DietHighSugar),
                ("DietLowFiber",       "Ít chất xơ",                "Ảnh hưởng tiêu hóa, tăng cholesterol xấu.",            "Ăn uống",  "bad", "fas fa-seedling",            h => h.DietLowFiber),
                ("AlcoholHeavy",       "Uống rượu nhiều",           "Gây tổn thương gan, tim mạch, thần kinh.",             "Ăn uống",  "bad", "fas fa-wine-bottle",         h => h.AlcoholHeavy),
                ("CaffeineSpike",      "Dùng caffeine quá mức",     "Gây tăng nhịp tim, lo âu, mất ngủ.",                   "Ăn uống",  "bad", "fas fa-mug-hot",             h => h.CaffeineSpike),
                // Nhóm 2: Sinh hoạt
                ("LifestyleSedentary", "Ít vận động",               "Tăng nguy cơ béo phì, tiểu đường, tim mạch.",          "Sinh hoạt","bad", "fas fa-couch",               h => h.LifestyleSedentary),
                ("LifestyleSitLong",   "Ngồi lâu một chỗ",         "Gây đau lưng, tuần hoàn máu kém, huyết khối.",         "Sinh hoạt","bad", "fas fa-chair",               h => h.LifestyleSitLong),
                ("SleepDeprived",      "Thiếu ngủ",                 "Suy giảm miễn dịch, tăng nguy cơ bệnh mãn tính.",     "Sinh hoạt","bad", "fas fa-bed",                 h => h.SleepDeprived),
                ("NoHealthCheck",      "Không khám định kỳ",        "Bỏ lỡ cơ hội phát hiện bệnh sớm.",                     "Sinh hoạt","bad", "fas fa-stethoscope",         h => h.NoHealthCheck),
                // Nhóm 3: Hành vi có hại
                ("SmokeActive",        "Hút thuốc chủ động",        "Nguyên nhân hàng đầu gây ung thư phổi, tim mạch.",     "Hành vi",  "bad", "fas fa-smoking",             h => h.SmokeActive),
                ("SmokePassive",       "Hút thuốc thụ động",        "Phơi nhiễm khói thuốc vẫn gây hại nghiêm trọng.",      "Hành vi",  "bad", "fas fa-wind",                h => h.SmokePassive),
                ("SelfMedication",     "Tự ý dùng thuốc",           "Tương tác thuốc nguy hiểm, nhờn kháng sinh.",           "Hành vi",  "bad", "fas fa-pills",               h => h.SelfMedication),
                // Nhóm 4: Tâm lý
                ("StressHigh",         "Căng thẳng kéo dài",        "Tăng cortisol, suy giảm miễn dịch, tim mạch.",         "Tâm lý",   "bad", "fas fa-brain",               h => h.StressHigh),
                // Nhóm 5: Thói quen tốt
                ("DietBalanced",       "Ăn uống đủ chất",           "Cung cấp đầy đủ vitamin, khoáng chất.",                "Ăn uống",  "good","fas fa-apple-whole",         h => h.DietBalanced),
                ("DrinkEnoughWater",   "Uống đủ nước",              "Giúp thanh lọc cơ thể, tốt cho tiêu hóa và da.",       "Ăn uống",  "good","fas fa-glass-water",         h => h.DrinkEnoughWater),
                ("ExerciseRegularly",  "Tập thể dục thường xuyên",  "Tăng cường sức khỏe tim mạch, cơ bắp, giảm stress.",   "Sinh hoạt","good","fas fa-person-running",      h => h.ExerciseRegularly),
                ("SleepEarly",         "Ngủ sớm, đủ giấc",          "Phục hồi năng lượng, tăng cường miễn dịch.",           "Sinh hoạt","good","fas fa-moon",                h => h.SleepEarly),
                ("RegularHealthCheck", "Khám sức khỏe điều độ",     "Phát hiện sớm và ngăn ngừa bệnh tật hiệu quả.",        "Sinh hoạt","good","fas fa-user-doctor",         h => h.RegularHealthCheck),
                ("NoSubstanceAbuse",   "Không dùng chất kích thích","Bảo vệ hệ thần kinh, gan, và phổi khỏe mạnh.",         "Hành vi",  "good","fas fa-ban",                 h => h.NoSubstanceAbuse),
            };

            // Tính count & percentage từng habit
            var habitItems = habitMeta.Select(m => new HabitItemViewModel
            {
                Key         = m.Key,
                Label       = m.Label,
                Description = m.Desc,
                Category    = m.Category,
                Type        = m.Type,
                Icon        = m.Icon,
                Count       = total > 0 ? habits.Count(m.Selector) : 0,
                Percentage  = total > 0 ? Math.Round(habits.Count(m.Selector) * 100.0 / total, 1) : 0
            }).ToList();

            // Nhóm theo Category
            var categoryMeta = new[]
            {
                new { Name = "Ăn uống",   Icon = "fas fa-utensils",             ColorClass = "danger"  },
                new { Name = "Sinh hoạt", Icon = "fas fa-person-walking",        ColorClass = "warning" },
                new { Name = "Hành vi",   Icon = "fas fa-skull-crossbones",      ColorClass = "secondary" },
                new { Name = "Tâm lý",    Icon = "fas fa-brain",                 ColorClass = "info"    },
            };

            var categories = categoryMeta.Select(cm => new HabitCategoryViewModel
            {
                Name       = cm.Name,
                Icon       = cm.Icon,
                ColorClass = cm.ColorClass,
                Items      = habitItems.Where(i => i.Category == cm.Name).OrderByDescending(i => i.Count).ToList()
            }).ToList();

            // Top 5 thói quen xấu phổ biến nhất
            var topBad = habitItems
                .Where(i => i.Type == "bad")
                .OrderByDescending(i => i.Count)
                .Take(5)
                .ToList();

            // Top 5 thói quen tốt phổ biến nhất
            var topGood = habitItems
                .Where(i => i.Type == "good")
                .OrderByDescending(i => i.Count)
                .Take(5)
                .ToList();

            // Phân bố: mỗi bệnh nhân có bao nhiêu thói quen xấu (0 → 14)
            var badSelectors = habitMeta.Where(m => m.Type == "bad").Select(m => m.Selector).ToList();
            var distribution = Enumerable.Range(0, badSelectors.Count + 1)
                .ToDictionary(i => $"{i}", _ => 0);

            foreach (var h in habits)
            {
                int badCount = badSelectors.Count(sel => sel(h));
                string key = $"{badCount}";
                if (distribution.ContainsKey(key)) distribution[key]++;
            }

            return new HabitStatisticsViewModel
            {
                TotalPatientsWithHabit     = total,
                TotalPatients              = totalPatients,
                Categories                 = categories,
                TopBadHabitLabels          = topBad.Select(x => x.Label).ToList(),
                TopBadHabitValues          = topBad.Select(x => x.Count).ToList(),
                TopGoodHabitLabels         = topGood.Select(x => x.Label).ToList(),
                TopGoodHabitValues         = topGood.Select(x => x.Count).ToList(),
                BadHabitDistributionLabels = distribution.Keys.ToList(),
                BadHabitDistributionValues = distribution.Values.ToList()
            };
        }
    }
}
