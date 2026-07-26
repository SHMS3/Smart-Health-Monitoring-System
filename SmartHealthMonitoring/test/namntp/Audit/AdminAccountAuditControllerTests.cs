using Microsoft.EntityFrameworkCore;
using Moq;
using SmartHealthMonitoring.Controllers.Admin;
using SmartHealthMonitoring.Interfaces;

namespace SmartHealthMonitoring.UnitTests;

public class AdminAccountAuditControllerTests
{
    [Fact]
    public async Task PatientToggleLock_LocksAndUnlocksWithAudit()
    {
        await using var context = TestContextFactory.Create();
        var patientUser = EntityFactory.User(1, 0, "Patient Lan");
        context.Users.Add(patientUser);
        await context.SaveChangesAsync();
        var audit = AuditMock();
        var controller = new AdminPatientController(context, audit.Object)
            .WithUser(99, roles: ["2"]);

        await controller.ToggleLock(patientUser.Id, "Policy violation");
        Assert.True((await context.Users.FindAsync(patientUser.Id))!.IsDeleted);
        Assert.Equal("Policy violation", patientUser.LockReason);
        audit.Verify(service => service.LogAsync(
            "Lock",
            "PatientAccount",
            patientUser.Id.ToString(),
            It.Is<string>(description => description.Contains("Policy violation")),
            patientUser.Id,
            patientUser.FullName), Times.Once);

        await controller.ToggleLock(patientUser.Id, null);
        Assert.False(patientUser.IsDeleted);
        Assert.Null(patientUser.LockReason);
        audit.Verify(service => service.LogAsync(
            "Unlock",
            "PatientAccount",
            patientUser.Id.ToString(),
            It.IsAny<string>(),
            patientUser.Id,
            patientUser.FullName), Times.Once);
    }

    [Fact]
    public async Task DoctorToggleLock_LocksAndUnlocksWithAudit()
    {
        await using var context = TestContextFactory.Create();
        var doctorUser = EntityFactory.User(1, 1, "Doctor Minh");
        context.Users.Add(doctorUser);
        await context.SaveChangesAsync();
        var audit = AuditMock();
        var email = new Mock<IEmailService>();
        var controller = new AdminDoctorController(context, audit.Object, email.Object)
            .WithUser(99, roles: ["2"]);

        await controller.ToggleLock(doctorUser.Id, null);
        Assert.True(doctorUser.IsDeleted);
        Assert.False(string.IsNullOrWhiteSpace(doctorUser.LockReason));
        audit.Verify(service => service.LogAsync(
            "Lock",
            "DoctorAccount",
            doctorUser.Id.ToString(),
            It.IsAny<string>(),
            doctorUser.Id,
            doctorUser.FullName), Times.Once);

        await controller.ToggleLock(doctorUser.Id, null);
        Assert.False(doctorUser.IsDeleted);
        Assert.Null(doctorUser.LockReason);
        audit.Verify(service => service.LogAsync(
            "Unlock",
            "DoctorAccount",
            doctorUser.Id.ToString(),
            It.IsAny<string>(),
            doctorUser.Id,
            doctorUser.FullName), Times.Once);
    }

    private static Mock<IAuditLogService> AuditMock()
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
        return audit;
    }
}
