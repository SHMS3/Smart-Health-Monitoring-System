using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartHealthMonitoring.Controllers.Admin;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class StandardThresholdControllerTests
{
    [Fact]
    public async Task Create_WhenThresholdRelationshipsAreInvalid_ReturnsValidationView()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context);
        var model = ValidModel();
        model.AgeMin = 60;
        model.AgeMax = 20;
        model.SystolicBpWarning = model.SystolicBpDanger;

        var result = await setup.Controller.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(setup.Controller.ModelState.IsValid);
        Assert.Empty(context.StandardThresholds);
    }

    [Fact]
    public async Task Create_MapsEntityAndWritesAudit()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context);
        var model = ValidModel();

        var result = await setup.Controller.Create(model);

        Assert.IsType<RedirectToActionResult>(result);
        var entity = await context.StandardThresholds.SingleAsync();
        Assert.Equal(model.Name, entity.Name);
        Assert.Equal(model.SystolicBpDanger, entity.SystolicBpDanger);
        setup.Audit.Verify(service => service.LogAsync(
            "Create",
            "StandardThreshold",
            entity.Id.ToString(),
            It.IsAny<string>(),
            null,
            model.Name), Times.Once);
    }

    [Fact]
    public async Task Edit_UpdatesEntityAndWritesAudit()
    {
        await using var context = TestContextFactory.Create();
        var entity = Threshold(1, "Old");
        context.StandardThresholds.Add(entity);
        await context.SaveChangesAsync();
        var setup = CreateController(context);
        var model = ValidModel();
        model.Name = "Updated";
        model.IsActive = false;

        var result = await setup.Controller.Edit(entity.Id, model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Updated", entity.Name);
        Assert.False(entity.IsActive);
        setup.Audit.Verify(service => service.LogAsync(
            "Update",
            "StandardThreshold",
            entity.Id.ToString(),
            It.Is<string>(description => description.Contains("Old")),
            null,
            "Updated"), Times.Once);
    }

    [Fact]
    public async Task ToggleActive_ChangesStateAndAuditsAction()
    {
        await using var context = TestContextFactory.Create();
        var entity = Threshold(1, "Adult", isActive: true);
        context.StandardThresholds.Add(entity);
        await context.SaveChangesAsync();
        var setup = CreateController(context);

        await setup.Controller.ToggleActive(entity.Id);

        Assert.False(entity.IsActive);
        setup.Audit.Verify(service => service.LogAsync(
            "Deactivate",
            "StandardThreshold",
            entity.Id.ToString(),
            It.IsAny<string>(),
            null,
            entity.Name), Times.Once);
    }

    [Fact]
    public async Task Delete_RemovesEntityAndWritesAudit()
    {
        await using var context = TestContextFactory.Create();
        var entity = Threshold(1, "Adult");
        context.StandardThresholds.Add(entity);
        await context.SaveChangesAsync();
        var setup = CreateController(context);

        await setup.Controller.Delete(entity.Id);

        Assert.Empty(context.StandardThresholds);
        setup.Audit.Verify(service => service.LogAsync(
            "Delete",
            "StandardThreshold",
            entity.Id.ToString(),
            It.IsAny<string>(),
            null,
            entity.Name), Times.Once);
    }

    private static ControllerSetup CreateController(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context)
    {
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(service => service.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var controller = new StandardThresholdController(context, audit.Object)
            .WithUser(99, roles: ["2"]);
        return new ControllerSetup(controller, audit);
    }

    private static StandardThresholdViewModel ValidModel()
    {
        return new StandardThresholdViewModel
        {
            Name = "Adult",
            Sex = 2,
            AgeMin = 18,
            AgeMax = 65,
            SystolicBpWarning = 130,
            SystolicBpDanger = 140,
            DiastolicBpWarning = 80,
            DiastolicBpDanger = 90,
            HeartRateDangerMin = 50,
            HeartRateWarningMin = 60,
            HeartRateWarningMax = 100,
            HeartRateDangerMax = 120,
            IsActive = true
        };
    }

    private static StandardThreshold Threshold(
        int id,
        string name,
        bool isActive = true)
    {
        return new StandardThreshold
        {
            Id = id,
            Name = name,
            IsActive = isActive
        };
    }

    private sealed record ControllerSetup(
        StandardThresholdController Controller,
        Mock<IAuditLogService> Audit);
}
