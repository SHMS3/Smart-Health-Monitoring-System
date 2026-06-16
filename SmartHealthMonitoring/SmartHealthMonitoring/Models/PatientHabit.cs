using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models
{
    [Table("PatientHabit")]
    public class PatientHabit
    {
        [Key]
        public int Id { get; set; }

        public int PatientId { get; set; }

        // Nhóm 1: Thói quen ăn uống (Dietary Habits)
        public bool DietSalty { get; set; }
        public bool DietHighFat { get; set; }
        public bool DietHighSugar { get; set; }
        public bool DietLowFiber { get; set; }
        public bool AlcoholHeavy { get; set; }
        public bool CaffeineSpike { get; set; }

        // Nhóm 2: Thói quen sinh hoạt (Lifestyle Habits)
        public bool LifestyleSedentary { get; set; }
        public bool LifestyleSitLong { get; set; }
        public bool SleepDeprived { get; set; }
        public bool NoHealthCheck { get; set; }

        // Nhóm 3: Các hành vi có hại (Harmful Behaviors)
        public bool SmokeActive { get; set; }
        public bool SmokePassive { get; set; }
        public bool SelfMedication { get; set; }

        // Nhóm 4: Yếu tố tâm lý (Psychological Factors)
        public bool StressHigh { get; set; }

        public DateTime UpdatedAt { get; set; }

        [ForeignKey("PatientId")]
        public virtual Patient Patient { get; set; } = null!;
    }
}
