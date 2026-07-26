using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Workers;

namespace SmartHealthMonitoring.UnitTests;

public class DailyVitalLogReminderWorkerTests
{
    [Fact]
    public async Task CheckAndSendRemindersAsync_ForStalePatient_CreatesReminder()
    {
        await using var context = TestContextFactory.Create();
        var user = EntityFactory.User(10, 0, "Patient Lan");
        user.CreatedAt = DateTime.Now.AddHours(-2);
        var patient = EntityFactory.Patient(1, user);
        context.Users.Add(user);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var trigger = TriggerMock();
        var worker = CreateWorker(context, trigger.Object);

        await InvokeCheckAsync(worker);

        trigger.Verify(service => service.SendDailyVitalLogReminderAsync(
            patient.Id,
            It.Is<string>(display => !string.IsNullOrWhiteSpace(display))), Times.Once);
    }

    [Fact]
    public async Task CheckAndSendRemindersAsync_WhenReminderAlreadyExists_DoesNotDuplicate()
    {
        await using var context = TestContextFactory.Create();
        var user = EntityFactory.User(10, 0, "Patient Lan");
        user.CreatedAt = DateTime.Now.AddHours(-2);
        var patient = EntityFactory.Patient(1, user);
        context.Users.Add(user);
        context.Patients.Add(patient);
        context.EmailNotifications.Add(new EmailNotification
        {
            Id = 1,
            PatientId = patient.Id,
            Patient = patient,
            ToEmail = user.Email,
            Subject = "Reminder",
            Body = "Body",
            Status = 3,
            CreatedAt = DateTime.Now.AddMinutes(-10)
        });
        await context.SaveChangesAsync();
        var trigger = TriggerMock();
        var worker = CreateWorker(context, trigger.Object);

        await InvokeCheckAsync(worker);

        trigger.Verify(service => service.SendDailyVitalLogReminderAsync(
            It.IsAny<int>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndSendRemindersAsync_WithRecentVitalLog_DoesNotRemind()
    {
        await using var context = TestContextFactory.Create();
        var user = EntityFactory.User(10, 0, "Patient Lan");
        user.CreatedAt = DateTime.Now.AddDays(-1);
        var patient = EntityFactory.Patient(1, user);
        context.Users.Add(user);
        context.Patients.Add(patient);
        context.DailyVitalLogs.Add(new DailyVitalLog
        {
            Id = 1,
            PatientId = patient.Id,
            Patient = patient,
            LoggedAt = DateTime.Now.AddMinutes(-10)
        });
        await context.SaveChangesAsync();
        var trigger = TriggerMock();
        var worker = CreateWorker(context, trigger.Object);

        await InvokeCheckAsync(worker);

        trigger.Verify(service => service.SendDailyVitalLogReminderAsync(
            It.IsAny<int>(),
            It.IsAny<string>()), Times.Never);
    }

    private static DailyVitalLogReminderWorker CreateWorker(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context,
        IEmailTriggerService trigger)
    {
        var services = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton(trigger)
            .BuildServiceProvider();
        return new DailyVitalLogReminderWorker(
            services,
            NullLogger<DailyVitalLogReminderWorker>.Instance);
    }

    private static Mock<IEmailTriggerService> TriggerMock()
    {
        var trigger = new Mock<IEmailTriggerService>();
        trigger
            .Setup(service => service.SendDailyVitalLogReminderAsync(
                It.IsAny<int>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return trigger;
    }

    private static async Task InvokeCheckAsync(DailyVitalLogReminderWorker worker)
    {
        var method = typeof(DailyVitalLogReminderWorker).GetMethod(
            "CheckAndSendRemindersAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(
            method.Invoke(worker, [CancellationToken.None]));
        await task;
    }
}
