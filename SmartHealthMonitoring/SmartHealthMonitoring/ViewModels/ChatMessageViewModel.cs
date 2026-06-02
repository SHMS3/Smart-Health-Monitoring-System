namespace SmartHealthMonitoring.ViewModels
{
    public class ChatMessageViewModel
    {
        public int Id { get; set; }

        public byte SenderRole { get; set; }

        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }
    }
}
