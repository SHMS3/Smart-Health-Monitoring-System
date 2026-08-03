using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services.AI;
using SmartHealthMonitoring.Workers.AI;

namespace SmartHealthMonitoring.UnitTests;

public class AiPredictionWorkerTests
{
    [Fact]
    public async Task DoWorkAsync_ForHighRiskDailyLog_PersistsAlertAndTriggersHealthWarning()
    {
        await using var context = CreateContext();
        var patient = await AddHighRiskDailyLogAsync(context);
        var aiService = PredictionService(0.85m);
        var trigger = TriggerService();
        var worker = CreateWorker(
            context,
            aiService.Object,
            trigger.Object,
            Mock.Of<IEmailService>());

        await InvokeDoWorkAsync(worker);

        var prediction = Assert.Single(context.AiriskPredictions);
        Assert.Equal(patient.Id, prediction.PatientId);
        Assert.Equal((byte)3, prediction.RiskLevel);
        Assert.Single(context.WarningAlerts);
        trigger.Verify(service => service.SendHealthWarningAsync(
            patient.Id,
            prediction.Id), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_ForDashboardHighRiskLevelTwo_TriggersHealthWarning()
    {
        await using var context = CreateContext();
        var patient = await AddHighRiskDailyLogAsync(context);
        var aiService = PredictionService(0.50m);
        var trigger = TriggerService();
        var worker = CreateWorker(
            context,
            aiService.Object,
            trigger.Object,
            Mock.Of<IEmailService>());

        await InvokeDoWorkAsync(worker);

        var prediction = Assert.Single(context.AiriskPredictions);
        Assert.Equal((byte)2, prediction.RiskLevel);
        trigger.Verify(service => service.SendHealthWarningAsync(
            patient.Id,
            prediction.Id), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_WhenHealthWarningFails_KeepsPersistedPredictionAndAlert()
    {
        await using var context = CreateContext();
        var patient = await AddHighRiskDailyLogAsync(context);
        var aiService = PredictionService(0.9m);
        var trigger = TriggerService();
        trigger
            .Setup(service => service.SendHealthWarningAsync(
                patient.Id,
                It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));
        var worker = CreateWorker(
            context,
            aiService.Object,
            trigger.Object,
            Mock.Of<IEmailService>());

        await InvokeDoWorkAsync(worker);

        Assert.Single(context.AiriskPredictions);
        Assert.Single(context.WarningAlerts);
        trigger.Verify(service => service.SendHealthWarningAsync(
            patient.Id,
            It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_ForExistingHighRiskAlertWithOnlyDoctorEmail_TriggersAutomaticHealthWarning()
    {
        await using var context = CreateContext();
        var graph = await AddExistingHighRiskAlertAsync(context, sentByDoctorId: 2);
        var trigger = TriggerService();
        var worker = CreateWorker(
            context,
            Mock.Of<IAiPredictionService>(),
            trigger.Object,
            Mock.Of<IEmailService>());

        await InvokeDoWorkAsync(worker);

        trigger.Verify(service => service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_ForExistingHighRiskAlertWithAutomaticEmail_DoesNotSendDuplicate()
    {
        await using var context = CreateContext();
        var graph = await AddExistingHighRiskAlertAsync(context, sentByDoctorId: null);
        var trigger = TriggerService();
        var worker = CreateWorker(
            context,
            Mock.Of<IAiPredictionService>(),
            trigger.Object,
            Mock.Of<IEmailService>());

        await InvokeDoWorkAsync(worker);

        trigger.Verify(service => service.SendHealthWarningAsync(
            It.IsAny<int>(),
            It.IsAny<int>()), Times.Never);
    }

    private static async Task<(Patient Patient, AiriskPrediction Prediction)>
        AddExistingHighRiskAlertAsync(
            SmartHealthMonitoringContext context,
            int? sentByDoctorId)
    {
        var patientUser = EntityFactory.User(30, 0, "Existing Patient");
        var patient = EntityFactory.Patient(3, patientUser);
        var prediction = new AiriskPrediction
        {
            Id = 31,
            PatientId = patient.Id,
            Patient = patient,
            RiskScore = 0.90m,
            PredictedTarget = 1,
            RiskLevel = 2,
            ModelVersion = "manual-sql",
            PredictedAt = new DateTime(2026, 8, 3, 15, 0, 0)
        };
        var alert = new WarningAlert
        {
            Id = 32,
            PatientId = patient.Id,
            Patient = patient,
            PredictionId = prediction.Id,
            Prediction = prediction,
            Status = 0,
            FlaggedAt = prediction.PredictedAt
        };
        var notification = new EmailNotification
        {
            Id = 33,
            AlertId = alert.Id,
            Alert = alert,
            PatientId = patient.Id,
            Patient = patient,
            ToEmail = patientUser.Email,
            Subject = "Existing email",
            Body = "Existing email body",
            Status = 1,
            IsSent = true,
            SentByDoctorId = sentByDoctorId,
            CreatedAt = prediction.PredictedAt
        };

        context.Users.Add(patientUser);
        context.Patients.Add(patient);
        context.AiriskPredictions.Add(prediction);
        context.WarningAlerts.Add(alert);
        context.EmailNotifications.Add(notification);
        await context.SaveChangesAsync();

        return (patient, prediction);
    }
    private static async Task<Patient> AddHighRiskDailyLogAsync(
        SmartHealthMonitoringContext context)
    {
        var patientUser = EntityFactory.User(10, 0, "Patient Lan");
        var patient = EntityFactory.Patient(1, patientUser);
        var doctorUser = EntityFactory.User(20, 1, "Doctor Minh");
        var doctor = EntityFactory.Doctor(2, doctorUser);
        doctor.IsOnShift = true;
        var log = new DailyVitalLog
        {
            Id = 11,
            PatientId = patient.Id,
            Patient = patient,
            LoggedAt = new DateTime(2026, 7, 26, 8, 0, 0),
            SystolicBp = 120,
            DiastolicBp = 80,
            HeartRate = 75,
            ChestPainLevel = 0,
            HasExerciseAngina = false
        };

        context.Users.AddRange(patientUser, doctorUser);
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        context.DailyVitalLogs.Add(log);
        await context.SaveChangesAsync();
        return patient;
    }

    private static Mock<IAiPredictionService> PredictionService(decimal riskScore)
    {
        var service = new Mock<IAiPredictionService>();
        service
            .Setup(item => item.PredictCombined(
                It.IsAny<DailyVitalLog>(),
                It.IsAny<ClinicalRecord?>(),
                It.IsAny<Patient>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>()))
            .Returns(() => new AiriskPrediction { RiskScore = riskScore });
        return service;
    }

    private static Mock<IEmailTriggerService> TriggerService()
    {
        var service = new Mock<IEmailTriggerService>();
        service
            .Setup(item => item.SendHealthWarningAsync(
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync(true);
        return service;
    }

    private static AiPredictionWorker CreateWorker(
        SmartHealthMonitoringContext context,
        IAiPredictionService aiService,
        IEmailTriggerService triggerService,
        IEmailService emailService)
    {
        var services = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton(aiService)
            .AddSingleton(triggerService)
            .AddSingleton(emailService)
            .BuildServiceProvider();
        return new AiPredictionWorker(
            services,
            NullLogger<AiPredictionWorker>.Instance);
    }

    private static SmartHealthMonitoringContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartHealthMonitoringContext>()
            .UseInMemoryDatabase($"AiPredictionWorkerTests-{Guid.NewGuid():N}")
            .Options;
        return new RowVersionContext(options);
    }

    private static async Task InvokeDoWorkAsync(AiPredictionWorker worker)
    {
        var method = typeof(AiPredictionWorker).GetMethod(
            "DoWorkAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(
            method.Invoke(worker, [CancellationToken.None]));
        await task;
    }

    private sealed class RowVersionContext(
        DbContextOptions<SmartHealthMonitoringContext> options)
        : SmartHealthMonitoringContext(options)
    {
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<WarningAlert>()
                         .Where(item => item.State == EntityState.Added))
            {
                entry.Entity.RowVersion = [1];
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
