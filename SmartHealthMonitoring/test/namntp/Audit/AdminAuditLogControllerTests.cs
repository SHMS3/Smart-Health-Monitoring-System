using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Controllers.Admin;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class AdminAuditLogControllerTests
{
    [Fact]
    public async Task Index_WhenDateRangeIsInvalid_ReturnsErrorsAndNoLogs()
    {
        await using var context = TestContextFactory.Create();
        context.Users.AddRange(
            EntityFactory.User(1, 0, "Patient"),
            EntityFactory.User(2, 1, "Doctor"),
            EntityFactory.User(3, 2, "Admin"));
        await context.SaveChangesAsync();
        var controller = new AdminAuditLogController(context).WithUser(3, roles: ["2"]);

        var result = await controller.Index(
            null,
            null,
            null,
            DateTime.Today,
            DateTime.Today.AddDays(-1),
            page: -5,
            pageSize: 500);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AuditLogIndexViewModel>(view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(model.Logs.Items);
        Assert.Equal(1, model.Logs.Page);
        Assert.Equal(100, model.Logs.PageSize);
        Assert.Equal([3, 2], model.Actors.Select(actor => actor.Id));
    }

    [Fact]
    public async Task Index_AppliesFiltersAndVietnamDateBoundary()
    {
        await using var context = TestContextFactory.Create();
        var localDate = new DateTime(2026, 7, 10);
        context.AuditLogs.AddRange(
            CreateLog(1, "Update", "Patient", 8, localDate.AddHours(-7)),
            CreateLog(2, "Update", "Patient", 8, localDate.AddDays(1).AddHours(-7).AddTicks(-1)),
            CreateLog(3, "Delete", "Patient", 8, localDate.AddHours(2)),
            CreateLog(4, "Update", "Doctor", 8, localDate.AddHours(2)),
            CreateLog(5, "Update", "Patient", 9, localDate.AddHours(2)));
        await context.SaveChangesAsync();
        var controller = new AdminAuditLogController(context).WithUser(1, roles: ["2"]);

        var result = await controller.Index(
            "Update",
            "Patient",
            8,
            localDate,
            localDate,
            page: 1,
            pageSize: 5);

        var model = Assert.IsType<AuditLogIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.Logs.TotalCount);
        Assert.Equal([2, 1], model.Logs.Items.Select(item => item.Id));
    }

    private static AuditLog CreateLog(
        int id,
        string action,
        string entity,
        int actorId,
        DateTime createdAt)
    {
        return new AuditLog
        {
            Id = id,
            ActorUserId = actorId,
            ActorName = $"Actor {actorId}",
            Action = action,
            EntityName = entity,
            Description = "Test",
            CreatedAt = createdAt
        };
    }
}
