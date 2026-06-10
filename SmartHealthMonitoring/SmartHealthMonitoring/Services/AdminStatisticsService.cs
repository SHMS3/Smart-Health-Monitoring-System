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
            foreach(var item in cpGroups)
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
            // Join patient to get age at time of clinical record. For simplicity, use current age.
            var patients40to50Ids = patients.Where(p => 
            {
                int age = today.Year - p.DateOfBirth.Year;
                if (p.DateOfBirth > today.AddYears(-age)) age--;
                return age >= 40 && age <= 50;
            }).Select(p => p.Id).ToList();

            var records40to50 = records.Where(r => patients40to50Ids.Contains(r.PatientId)).ToList();
            double avgCholesterol = records40to50.Any() ? records40to50.Average(r => r.Cholesterol) : 0;

            // FastingBs > 120mg/dl rate. In most datasets, 1 = true (>120), 0 = false.
            double highFastingBsRate = 0;
            if (records.Any())
            {
                int highCount = records.Count(r => r.FastingBs == 1);
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
    }
}
