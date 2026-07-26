using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class EmergencyContactViewModelTests
{
    [Fact]
    public void Validation_WithValidEmailOrVietnamesePhone_Succeeds()
    {
        var emailForm = ValidForm();
        var phoneForm = ValidForm();
        phoneForm.Email = null;
        phoneForm.Phone = "+84901234567";

        Assert.Empty(Validate(emailForm));
        Assert.Empty(Validate(phoneForm));
    }

    [Theory]
    [MemberData(nameof(InvalidForms))]
    public void Validation_WithInvalidBusinessRules_ReturnsExpectedMember(
        EmergencyContactFormViewModel form,
        string memberName)
    {
        var results = Validate(form);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(memberName));
    }

    public static IEnumerable<object[]> InvalidForms()
    {
        var noContactMethod = ValidForm();
        noContactMethod.Email = null;
        noContactMethod.Phone = null;
        yield return [noContactMethod, nameof(EmergencyContactFormViewModel.Email)];

        var numericName = ValidForm();
        numericName.FullName = "12345";
        yield return [numericName, nameof(EmergencyContactFormViewModel.FullName)];

        var unsafeName = ValidForm();
        unsafeName.FullName = "<script>";
        yield return [unsafeName, nameof(EmergencyContactFormViewModel.FullName)];

        var unsafeRelationship = ValidForm();
        unsafeRelationship.Relationship = "<parent>";
        yield return [unsafeRelationship, nameof(EmergencyContactFormViewModel.Relationship)];

        var inactivePrimary = ValidForm();
        inactivePrimary.IsPrimary = true;
        inactivePrimary.IsActive = false;
        yield return [inactivePrimary, nameof(EmergencyContactFormViewModel.IsActive)];
    }

    private static EmergencyContactFormViewModel ValidForm()
    {
        return new EmergencyContactFormViewModel
        {
            FullName = "Nguyen Van A",
            Relationship = "Brother",
            Email = "person@example.com",
            IsActive = true
        };
    }

    private static List<ValidationResult> Validate(EmergencyContactFormViewModel form)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            form,
            new ValidationContext(form),
            results,
            validateAllProperties: true);
        return results;
    }
}
