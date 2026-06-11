namespace SmartHealthMonitoring.ViewModels
{
    public class PersonalHealthTrackerViewModel
    {
        public int Days { get; set; } = 7;
        public List<string> Labels { get; set; } = new(); 
        public List<int> SystolicBpValues { get; set; } = new(); 
        public List<int> DiastolicBpValues { get; set; } = new(); 
        public List<int> HeartRateValues { get; set; } = new(); 
    }
}
