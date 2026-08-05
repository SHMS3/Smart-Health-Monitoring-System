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
                return new ValidationResult("Ng�y sinh kh�ng h?p l?.");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            if (dob < MinDate)
            {
                return new ValidationResult(
                    $"Ng�y sinh kh�ng h?p l?. Nam sinh ph?i t? {MinDate.Year} tr? di."
                );
            }

            if (dob > today)
            {
                return new ValidationResult(
                    "Ng�y sinh kh�ng h?p l?. Ng�y sinh kh�ng du?c l?n hon ng�y hi?n t?i."
                );
            }

            int age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;

            if (age > MaxAge)
            {
                return new ValidationResult(
                    $"Ng�y sinh kh�ng h?p l?. Tu?i kh�ng du?c vu?t qu� {MaxAge}."
                );
            }

            return ValidationResult.Success;
        }
    }
}
