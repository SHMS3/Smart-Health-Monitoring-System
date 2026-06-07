using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmartHealthMonitoring.ViewModels
{
    public class ClinicalExamFormViewModel
    {
        [Required]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại đau ngực.")]
        public byte ChestPainType { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Huyết áp nghỉ.")]
        [Range(40, 300, ErrorMessage = "CẢNH BÁO MÁY ĐO: Huyết áp {0} mmHg là phi thực tế (Chuẩn: 40-300). Vui lòng kiểm tra lại thiết bị!")]
        public short RestingBP { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Cholesterol.")]
        [Range(50, 800, ErrorMessage = "LỖI LIS: Chỉ số Cholesterol {0} mg/dl không hợp lệ. Hãy chạy lại mẫu sinh hóa!")]
        public short Cholesterol { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái Đường huyết.")]
        public byte FastingBS { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái ECG.")]
        public byte RestECG { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Nhịp tim tối đa.")]
        [Range(40, 250, ErrorMessage = "CẢNH BÁO MÁY ĐO: Nhịp tim {0} bpm nằm ngoài giới hạn sinh tồn (40-250). Có thể lỏng điện cực!")]
        public short MaxHeartRate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn triệu chứng Đau ngực gắng sức.")]
        public byte ExerciseAngina { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập độ chênh ST (Oldpeak).")]
        [Range(0.0, 10.0, ErrorMessage = "LỖI ĐIỆN TÂM ĐỒ: Độ dốc Oldpeak {0} là vô lý. Kiểm tra lại dải đo ECG!")]
        public decimal OldPeak { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Độ dốc ST.")]
        public byte STSlope { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số Mạch chính.")]
        public byte MajorVessels { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Khuyết tật Thal.")]
        public byte ThalResult { get; set; }

        public bool IsViewForPatient { get; set; }

        public string? EcgImageUrl { get; set; }

        public IFormFile? AttachmentFile { get; set; } // Dùng để hứng file từ thẻ <input type="file">

        public string? AttachmentUrl { get; set; }    // Dùng để lưu link sau khi up lên MinIO
    }
}