using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class ChangePasswordViewModelTests
{
    [Fact]
    public void Validation_WhenNewPasswordEqualsCurrent_ReturnsNewPasswordError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "same-password",
            NewPassword = "same-password",
            ConfirmNewPassword = "same-password"
        };

        var results = Validate(model);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(model.NewPassword)));
    }

    [Fact]
    public void Validation_WhenConfirmationDoesNotMatch_ReturnsConfirmationError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old-password",
            NewPassword = "new-password",
            ConfirmNewPassword = "different"
        };

        var results = Validate(model);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(model.ConfirmNewPassword)));
    }

    private static List<ValidationResult> Validate(ChangePasswordViewModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }
}
