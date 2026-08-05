namespace SmartHealthMonitoring.ViewModels.Home
{
    public class GenerateOtpRequest
    {
        public string Phone { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
