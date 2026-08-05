using System;
using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.Attributes;
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
        [Required(ErrorMessage = "H? v� t�n l� b?t bu?c.")]
        [StringLength(100, ErrorMessage = "H? v� t�n kh�ng du?c vu?t qu� 100 k� t?.")]
        [Display(Name = "H? v� t�n")]
        public string FullName { get; set; } = null!;   

        [Required(ErrorMessage = "Email l� b?t bu?c.")]
        [EmailAddress(ErrorMessage = "Email kh�ng h?p l?.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Ng�y sinh l� b?t bu?c.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ng�y sinh")]
        [ValidDateOfBirth]
        public DateOnly DateOfBirth { get; set; }

        [Required(ErrorMessage = "Gi?i t�nh l� b?t bu?c.")]
        [Display(Name = "Gi?i t�nh")]
        public byte Sex { get; set; }

        [Phone(ErrorMessage = "S? di?n tho?i kh�ng h?p l?.")]
        [StringLength(10, ErrorMessage = "S? di?n tho?i qu� d�i.")]
        [Required(ErrorMessage = "S? di?n tho?i l� b?t bu?c.")]
        [Display(Name = "S? di?n tho?i")]
        public string Phone { get; set; }

        [StringLength(255, ErrorMessage = "�?a ch? qu� d�i.")]
        [Display(Name = "�?a ch?")]
        public string? Address { get; set; }

        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD/CMND ph?i t? 9 d?n 12 k� t?.")]
        [Display(Name = "CCCD/CMND")]
        [Required(ErrorMessage = "CCCD l� b?t bu?c.")]
        public string? CitizenId { get; set; }
    }
}
