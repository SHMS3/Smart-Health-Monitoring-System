using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartHealthMonitoring.Controllers.AI;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.Services.AI;

namespace SmartHealthMonitoring.UnitTests;

public class WarningAlertControllerTests
{
    [Fact]
    public async Task Resolve_WhenEmailRequestedWithoutAppointmentDate_StopsBeforeResolve()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context);

        var result = await setup.Controller.Resolve(
            4,
            "Resolved",
            sendEmailInvitation: true,
            appointmentDate: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(WarningAlertController.Details), redirect.ActionName);
        setup.Warning.Verify(service => service.ResolveAlertAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>()), Times.Never);
        setup.Trigger.Verify(service => service.SendAppointmentInvitationAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_WhenThresholdValidationFails_DoesNotResolveAlert()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context);
        setup.Threshold
            .Setup(service => service.ValidateAndUpdateAsync(
                It.IsAny<WarningAlert>(),
                It.IsAny<int>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>()))
            .ReturnsAsync(ServiceResult.Fail("Invalid thresholds"));

        var result = await setup.Controller.Resolve(4, "Resolved");

        Assert.Equal(
            nameof(WarningAlertController.Details),
            Assert.IsType<RedirectToActionResult>(result).ActionName);
        setup.Warning.Verify(service => service.ResolveAlertAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_WithoutEmail_ResolvesAlertAndDoesNotCallEmailTrigger()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context);

        var result = await setup.Controller.Resolve(
            4,
            "Patient is stable",
            sendEmailInvitation: false);

        Assert.Equal(
            nameof(WarningAlertController.Dashboard),
            Assert.IsType<RedirectToActionResult>(result).ActionName);
        setup.Warning.Verify(service => service.ResolveAlertAsync(
            4,
            2,
            "Patient is stable"), Times.Once);
        setup.Trigger.Verify(service => service.SendAppointmentInvitationAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<DateTime?>()), Times.Never);
        Assert.NotNull(setup.Controller.TempData["Success"]);
    }

    [Fact]
    public async Task Resolve_WhenInvitationFails_ReturnsDashboardWithWarning()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context, emailSent: false);
        var appointmentDate = DateTime.Now.AddDays(2);

        var result = await setup.Controller.Resolve(
            4,
            "Return for examination",
            sendEmailInvitation: true,
            appointmentDate: appointmentDate);

        Assert.Equal(
            nameof(WarningAlertController.Dashboard),
            Assert.IsType<RedirectToActionResult>(result).ActionName);
        setup.Trigger.Verify(service => service.SendAppointmentInvitationAsync(
            4,
            2,
            appointmentDate), Times.Once);
        Assert.NotNull(setup.Controller.TempData["Warning"]);
    }

    [Fact]
    public async Task Resolve_WhenInvitationSucceeds_ReturnsDashboardWithSuccess()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context, emailSent: true);
        var appointmentDate = DateTime.Now.AddDays(2);

        var result = await setup.Controller.Resolve(
            4,
            "Return for examination",
            sendEmailInvitation: true,
            appointmentDate: appointmentDate);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(setup.Controller.TempData["Success"]);
        Assert.Null(setup.Controller.TempData["Warning"]);
    }

    private static ControllerSetup CreateController(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context,
        bool emailSent = true)
    {
        var doctor = EntityFactory.Doctor(2, EntityFactory.User(20, 1));
        var alert = new WarningAlert
        {
            Id = 4,
            PatientId = 1,
            PredictionId = 3,
            RowVersion = [1]
        };

        var warning = new Mock<IAiWarningAlertService>();
        warning
            .Setup(service => service.GetAlertForResolveAsync(alert.Id))
            .ReturnsAsync(alert);
        warning
            .Setup(service => service.ResolveAlertAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>()))
            .ReturnsAsync(ServiceResult.Ok());

        var doctorService = new Mock<IDoctorService>();
        doctorService
            .Setup(service => service.GetDoctorByUserIdAsync(doctor.UserId))
            .ReturnsAsync(doctor);

        var trigger = new Mock<IEmailTriggerService>();
        trigger
            .Setup(service => service.SendAppointmentInvitationAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(emailSent);

        var threshold = new Mock<IThresholdService>();
        threshold
            .Setup(service => service.ValidateAndUpdateAsync(
                It.IsAny<WarningAlert>(),
                It.IsAny<int>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>(),
                It.IsAny<short?>()))
            .ReturnsAsync(ServiceResult.Ok());

        var controller = new WarningAlertController(
            warning.Object,
            doctorService.Object,
            context,
            Mock.Of<IEmailService>(),
            trigger.Object,
            Mock.Of<IAuditLogService>(),
            Mock.Of<IAnfisExplainabilityService>(),
            threshold.Object)
            .WithUser(doctor.UserId, roles: ["1"]);

        return new ControllerSetup(controller, warning, trigger, threshold);
    }

    private sealed record ControllerSetup(
        WarningAlertController Controller,
        Mock<IAiWarningAlertService> Warning,
        Mock<IEmailTriggerService> Trigger,
        Mock<IThresholdService> Threshold);
}
