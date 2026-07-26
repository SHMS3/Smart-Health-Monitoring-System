using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.UnitTests;

public class ServiceResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessfulResultWithMessage()
    {
        var result = ServiceResult.Ok("saved");

        Assert.True(result.Success);
        Assert.Equal("saved", result.Message);
    }

    [Fact]
    public void Fail_ReturnsFailedResultWithMessage()
    {
        var result = ServiceResult.Fail("invalid");

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Message);
    }
}
