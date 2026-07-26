using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class PatientUiSettingsViewModelTests
{
    [Fact]
    public void FromSettings_InfersEveryEditableFooterItemKind()
    {
        var settings = new PatientUiSettings
        {
            FooterSections =
            [
                new PatientFooterSection
                {
                    DisplayType = PatientFooterSectionDisplayTypes.Contact,
                    Items =
                    [
                        new PatientFooterItem { Label = "Phone", Url = "tel:0901234567" },
                        new PatientFooterItem { Label = "Email", Url = "mailto:test@example.com" },
                        new PatientFooterItem { Label = "Address", IconClass = "fas fa-map-marker-alt" },
                        new PatientFooterItem { Label = "Website", IconClass = "fas fa-globe" },
                        new PatientFooterItem
                        {
                            Label = "Status",
                            IconClass = "fas fa-circle",
                            Highlight = true
                        },
                        new PatientFooterItem { Label = "Text", IconClass = "fas fa-circle" }
                    ]
                }
            ]
        };

        var model = PatientUiSettingsViewModel.FromSettings(settings);

        Assert.Equal(
            [
                PatientFooterItemKinds.Phone,
                PatientFooterItemKinds.Email,
                PatientFooterItemKinds.Address,
                PatientFooterItemKinds.Website,
                PatientFooterItemKinds.Status,
                PatientFooterItemKinds.Text
            ],
            model.ContactItems.Select(item => item.Kind));
    }

    [Fact]
    public void EnsureOptions_RepairsNullListsAndSelectsCurrentLogo()
    {
        var model = new PatientUiSettingsViewModel
        {
            LogoIcon = "fas fa-stethoscope",
            WorkScheduleItems = null!,
            ContactItems = null!
        };

        model.EnsureOptions();

        Assert.NotNull(model.WorkScheduleItems);
        Assert.NotNull(model.ContactItems);
        Assert.Equal(
            PatientUiSettingsViewModel.AllowedLogoIcons.Length,
            model.LogoIconOptions.Count);
        Assert.Equal(
            "fas fa-stethoscope",
            Assert.Single(model.LogoIconOptions, option => option.Selected).Value);
    }
}
