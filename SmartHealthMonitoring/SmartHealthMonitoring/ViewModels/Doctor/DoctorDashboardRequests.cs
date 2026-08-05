namespace SmartHealthMonitoring.ViewModels.Doctor
{
    public class CancelExamRequest
    {
        public int WaitingId { get; set; }
    }

    public class AcceptPatientRequest
    {
        public int WaitingId { get; set; }
    }

    public class CreatePaymentRequest
    {
        public int PatientId { get; set; }
        public System.Collections.Generic.List<int> ServiceIds { get; set; } = new();
        public string? Note { get; set; }
    }
}
