namespace SmartHealthMonitoring.Models;

public class AiAlertSetting
{
    public const int DefaultId = 1;

    public int Id { get; set; }

    public byte EmergencyRiskLevelThreshold { get; set; } = 3;

    public decimal EmergencyRiskScoreThreshold { get; set; } = 0.70m;

    public byte EmergencyAgeMin { get; set; } = 0;

    public byte EmergencyAgeMax { get; set; } = 120;

    public byte EmergencySex { get; set; } = 2;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedByAdminId { get; set; }

    public virtual User? UpdatedByAdmin { get; set; }
}
