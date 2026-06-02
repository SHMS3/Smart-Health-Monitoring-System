namespace SmartHealthMonitoring.ViewModels
{
    public class ChatSessionViewModel
    {
        public int SessionId { get; set; }

        public DateTime StartedAt { get; set; }

        public List<ChatMessageViewModel> Messages { get; set; }
            = new();
    }
}
