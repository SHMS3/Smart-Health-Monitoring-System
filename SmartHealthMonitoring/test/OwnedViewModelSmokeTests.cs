using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class OwnedViewModelSmokeTests
{
    [Fact]
    public void AdminPatientListViewModel_LockReason_RoundTrips()
    {
        var model = new AdminPatientListViewModel
        {
            LockReason = "Account review"
        };

        Assert.Equal("Account review", model.LockReason);
    }
}
