using Microsoft.Extensions.Caching.Memory;
using Moq;
using SmartHealthMonitoring.Controllers;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class ClinicalExamAuditTests
{
    [Fact]
    public async Task SettingPatientThreshold_WhenNew_CreatesThresholdAndAuditsPatient()
    {
        await using var context = TestContextFactory.Create();
        var graph = await AddPatientAndDoctorAsync(context);
        var setup = CreateController(context, graph.Doctor.UserId);
        var model = ValidModel(graph.Patient.Id);

        await setup.Controller.SettingPatientThreshold(model);

        var threshold = Assert.Single(context.PatientThresholds);
        Assert.Equal(graph.Doctor.Id, threshold.UpdatedByDoctorId);
        setup.Audit.Verify(service => service.LogAsync(
            "Create",
            "PatientThreshold",
            threshold.Id.ToString(),
            It.IsAny<string>(),
            graph.Patient.UserId,
            graph.Patient.User.FullName), Times.Once);
    }

    [Fact]
    public async Task SettingPatientThreshold_WhenExisting_UpdatesThresholdAndAudits()
    {
        await using var context = TestContextFactory.Create();
        var graph = await AddPatientAndDoctorAsync(context);
        var existing = new PatientThreshold
        {
            Id = 7,
            PatientId = graph.Patient.Id,
            Patient = graph.Patient,
            SystolicBpWarning = 125,
            SystolicBpDanger = 135
        };
        context.PatientThresholds.Add(existing);
        await context.SaveChangesAsync();
        var setup = CreateController(context, graph.Doctor.UserId);
        var model = ValidModel(graph.Patient.Id);
        model.SystolicBpWarning = 132;
        model.SystolicBpDanger = 145;

        await setup.Controller.SettingPatientThreshold(model);

        Assert.Equal(132, existing.SystolicBpWarning);
        Assert.Equal(145, existing.SystolicBpDanger);
        setup.Audit.Verify(service => service.LogAsync(
            "Update",
            "PatientThreshold",
            existing.Id.ToString(),
            It.IsAny<string>(),
            graph.Patient.UserId,
            graph.Patient.User.FullName), Times.Once);
    }

    private static ControllerSetup CreateController(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context,
        int doctorUserId)
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
        var controller = new ClinicalExamController(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IEmailService>(),
            Mock.Of<IMinioService>(),
            audit.Object)
            .WithUser(doctorUserId, roles: ["1"]);
        return new ControllerSetup(controller, audit);
    }

    private static PatientThresholdViewModel ValidModel(int patientId)
    {
        return new PatientThresholdViewModel
        {
            PatientId = patientId,
            SystolicBpWarning = 130,
            SystolicBpDanger = 140,
            DiastolicBpWarning = 80,
            DiastolicBpDanger = 90,
            HeartRateDangerMin = 50,
            HeartRateWarningMin = 60,
            HeartRateWarningMax = 100,
            HeartRateDangerMax = 120
        };
    }

    private static async Task<PatientDoctorGraph> AddPatientAndDoctorAsync(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context)
    {
        var patientUser = EntityFactory.User(10, 0, "Patient Lan");
        var patient = EntityFactory.Patient(1, patientUser);
        var doctorUser = EntityFactory.User(20, 1, "Doctor Minh");
        var doctor = EntityFactory.Doctor(2, doctorUser);
        context.Users.AddRange(patientUser, doctorUser);
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync();
        return new PatientDoctorGraph(patient, doctor);
    }

    private sealed record ControllerSetup(
        ClinicalExamController Controller,
        Mock<IAuditLogService> Audit);

    private sealed record PatientDoctorGraph(Patient Patient, Doctor Doctor);
}
