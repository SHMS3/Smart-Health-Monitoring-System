using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.ViewModels
{
    public class UserViewModel
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int RoleId { get; set; }
        public string? RoleName { get; set; }  
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
