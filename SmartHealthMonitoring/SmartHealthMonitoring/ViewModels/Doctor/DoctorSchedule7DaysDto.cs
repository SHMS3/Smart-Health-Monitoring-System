namespace SmartHealthMonitoring.ViewModels.Doctor;

public class DoctorSchedule7DaysDto
{
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public int SlotDurationMinutes { get; set; } = 30;
}

public class DoctorSchedule7DaysViewModel
{
    public DateTime Date { get; set; }
    public List<DoctorSchedule7DaysDto> Blocks { get; set; } = new();
}
