using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class EmailHistoryViewModelTests
{
    [Theory]
    [InlineData(0, 1, 10, 0, 0)]
    [InlineData(23, 2, 10, 11, 20)]
    [InlineData(23, 3, 10, 21, 23)]
    public void PagingProperties_ReturnExpectedRange(
        int total,
        int page,
        int pageSize,
        int expectedStart,
        int expectedEnd)
    {
        var model = new EmailHistoryIndexViewModel
        {
            TotalItems = total,
            CurrentPage = page,
            PageSize = pageSize
        };

        Assert.Equal(expectedStart, model.StartItem);
        Assert.Equal(expectedEnd, model.EndItem);
    }
}
