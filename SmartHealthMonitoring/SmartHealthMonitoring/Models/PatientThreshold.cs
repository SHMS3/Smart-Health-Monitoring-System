using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.Models
{
    public class PatientThreshold
    {
        [Key]
        public int Id { get; set; }

        public int PatientId { get; set; }

        public short SystolicBpWarning { get; set; } = 130; // T? m?c n�y l� C?nh b�o
        public short SystolicBpDanger { get; set; } = 140;  // T? m?c n�y l� Nguy hi?m

        public short DiastolicBpWarning { get; set; } = 80;
        public short DiastolicBpDanger { get; set; } = 90;

        public short HeartRateWarningMin { get; set; } = 60;  // Du?i m?c n�y l� C?nh b�o
        public short HeartRateDangerMin { get; set; } = 50;   // Du?i m?c n�y l� Nguy hi?m

        public short HeartRateWarningMax { get; set; } = 100; // Tr�n m?c n�y l� C?nh b�o
        public short HeartRateDangerMax { get; set; } = 120;  // Tr�n m?c n�y l� Nguy hi?m

        public DateTime UpdatedAt { get; set; }

        public int? UpdatedByDoctorId { get; set; }

        public virtual Patient Patient { get; set; } = null!;
        public virtual Doctor? UpdatedByDoctor { get; set; }
    }
}
