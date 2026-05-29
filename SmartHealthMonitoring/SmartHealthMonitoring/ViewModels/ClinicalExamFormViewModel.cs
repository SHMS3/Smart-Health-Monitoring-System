using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ClinicalExamFormViewModel
    {
        [Required]
        public int PatientId { get; set; }

        public byte ChestPainType { get; set; }
        public short RestingBP { get; set; }
        public short Cholesterol { get; set; }

        // Chú ý 2 dòng này, chữ BS và ECG viết hoa toàn bộ
        public byte FastingBS { get; set; }
        public byte RestECG { get; set; }

        public short MaxHeartRate { get; set; }
        public byte ExerciseAngina { get; set; }
        public decimal OldPeak { get; set; }
        public byte STSlope { get; set; }
        public byte MajorVessels { get; set; }
        public byte ThalResult { get; set; }
        public bool IsViewForPatient { get; set; }
    }
}
