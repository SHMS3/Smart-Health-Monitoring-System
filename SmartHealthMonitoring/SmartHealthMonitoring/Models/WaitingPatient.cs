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
        
        /// <summary>
        /// ID của nhân viên lễ tân (User ID có Role=2) đã đăng ký khám
        /// </summary>
        public int ReceptionistId { get; set; }
        
        /// <summary>
        /// ID của bác sĩ (Doctor ID) đã tiếp nhận khám, nullable
        /// </summary>
        public int? DoctorId { get; set; }
        
        /// <summary>
        /// Số thứ tự chờ khám trong ngày
        /// </summary>
        public int SequenceNumber { get; set; }
        
        /// <summary>
        /// Trạng thái: 0 = Đang chờ, 1 = Đã tiếp nhận, 2 = Đã hủy
        /// </summary>
        public int Status { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? AcceptedAt { get; set; }

        public virtual Patient Patient { get; set; } = null!;
        public virtual User Receptionist { get; set; } = null!;
        public virtual Doctor? Doctor { get; set; }
    }
}
