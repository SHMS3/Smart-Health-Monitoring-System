namespace SmartHealthMonitoring.ViewModels
{
    public class LabWebhookPayload
    {
        public int PatientId { get; set; }
        public byte ChestPainType { get; set; }
        public short RestingBP { get; set; }
        public short Cholesterol { get; set; }
        public byte FastingBS { get; set; }
        public byte RestECG { get; set; }
        public short MaxHeartRate { get; set; }
        public byte ExerciseAngina { get; set; }
        public decimal OldPeak { get; set; }
        public byte STSlope { get; set; }
        public byte MajorVessels { get; set; }
        public byte ThalResult { get; set; }
        public string? EcgImageBase64 { get; set; } // Hứng chuỗi mã hóa ảnh từ máy xét nghiệm đẩy sang
        public string? EcgImageUrl { get; set; }     // Trả về link Presigned URL của MinIO cho Client
    }
}
