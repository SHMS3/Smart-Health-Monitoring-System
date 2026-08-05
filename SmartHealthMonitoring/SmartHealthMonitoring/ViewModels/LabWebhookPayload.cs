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
        public string? EcgImageBase64 { get; set; } // H?ng chu?i m� h�a ?nh t? m�y x�t nghi?m d?y sang
        public string? EcgImageUrl { get; set; }     // Tr? v? link Presigned URL c?a MinIO cho Client
    }
}
