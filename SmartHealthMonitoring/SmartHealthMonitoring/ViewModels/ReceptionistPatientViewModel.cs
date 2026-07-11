using System;
using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.ViewModels
{
    public class ReceptionistPatientListViewModel
    {
        public PagedResult<Patient> Patients { get; set; } = new PagedResult<Patient>();
        public string? SearchQuery { get; set; }
    }

    public class ReceptionistRegisterPatientViewModel
    {
        [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Ngày sinh là bắt buộc.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateOnly DateOfBirth { get; set; }

        [Required(ErrorMessage = "Giới tính là bắt buộc.")]
        [Display(Name = "Giới tính")]
        public byte Sex { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(15, ErrorMessage = "Số điện thoại quá dài.")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [StringLength(255, ErrorMessage = "Địa chỉ quá dài.")]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD/CMND phải từ 9 đến 12 ký tự.")]
        [Display(Name = "CCCD/CMND")]
        public string? CitizenId { get; set; }
    }
}
