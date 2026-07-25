using System;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.Attributes
{
  
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ValidDateOfBirthAttribute : ValidationAttribute
    {
        private static readonly DateOnly MinDate = new DateOnly(1900, 1, 1);
        private const int MaxAge = 150;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            DateOnly dob;

            if (value is DateOnly dateOnly)
            {
                dob = dateOnly;
            }
            else if (value is DateTime dateTime)
            {
                dob = DateOnly.FromDateTime(dateTime);
            }
            else
            {
                return new ValidationResult("Ngày sinh không hợp lệ.");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            if (dob < MinDate)
            {
                return new ValidationResult(
                    $"Ngày sinh không hợp lệ. Năm sinh phải từ {MinDate.Year} trở đi."
                );
            }

            if (dob > today)
            {
                return new ValidationResult(
                    "Ngày sinh không hợp lệ. Ngày sinh không được lớn hơn ngày hiện tại."
                );
            }

            int age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;

            if (age > MaxAge)
            {
                return new ValidationResult(
                    $"Ngày sinh không hợp lệ. Tuổi không được vượt quá {MaxAge}."
                );
            }

            return ValidationResult.Success;
        }
    }
}
