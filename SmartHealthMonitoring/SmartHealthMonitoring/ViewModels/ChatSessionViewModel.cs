namespace SmartHealthMonitoring.ViewModels
{
    public class AiChatSessionViewModel
    {
        public int SessionId { get; set; }

        public DateTime StartedAt { get; set; }

        public List<ChatMessageViewModel> Messages { get; set; }
            = new();
    }

    public class ChatSessionViewModel
    {
        public int SessionId { get; set; }
        public int PatientUserId { get; set; }
        public string PatientName { get; set; } = null!;
        public int? DoctorUserId { get; set; }
        public string? DoctorName { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }
}
