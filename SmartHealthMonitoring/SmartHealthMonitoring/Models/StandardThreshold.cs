using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.Models
{
    public class StandardThreshold
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public byte Sex { get; set; } = 2;

        public byte AgeMin { get; set; } = 0;

        public byte AgeMax { get; set; } = 120;

        public short SystolicBpWarning { get; set; } = 130;
        public short SystolicBpDanger  { get; set; } = 140;

        public short DiastolicBpWarning { get; set; } = 80;
        public short DiastolicBpDanger  { get; set; } = 90;

        public short HeartRateWarningMin { get; set; } = 60;
        public short HeartRateDangerMin  { get; set; } = 50;
        public short HeartRateWarningMax { get; set; } = 100;
        public short HeartRateDangerMax  { get; set; } = 120;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

        public DateTime UpdatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;
    }
}
