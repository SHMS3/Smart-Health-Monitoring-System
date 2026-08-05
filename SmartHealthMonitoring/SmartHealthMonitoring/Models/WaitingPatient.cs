using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models
{
    public class WaitingPatient
    {
        [Key]
        public int Id { get; set; }
        
        public int PatientId { get; set; }
        
        public int ReceptionistId { get; set; }
        
        public int? DoctorId { get; set; }
        
        public int SequenceNumber { get; set; }
        
        public int Status { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? AcceptedAt { get; set; }

        public virtual Patient Patient { get; set; } = null!;
        public virtual User Receptionist { get; set; } = null!;
        public virtual Doctor? Doctor { get; set; }
    }
}
