using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.UnitTests;

public class EmailTriggerServiceTests
{
    [Fact]
    public async Task SendAppointmentInvitationAsync_WhenEmailSucceeds_TracksSuccessfulNotification()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 3);
        var setup = CreateService(context, temp.Path);

        var result = await setup.Service.SendAppointmentInvitationAsync(
            graph.Alert.Id,
            graph.Doctor.Id,
            new DateTime(2026, 8, 2, 9, 30, 0));

        Assert.True(result);
        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal(graph.Alert.Id, notification.AlertId);
        Assert.Equal(graph.Doctor.Id, notification.SentByDoctorId);
        Assert.Equal((byte)1, notification.Status);
        Assert.True(notification.IsSent);
        Assert.NotNull(notification.SentAt);
        setup.Email.Verify(service => service.GetHtmlContentFromFile(
            "AppointmentInvitationTemplate.html",
            It.Is<Dictionary<string, string>>(values =>
                values["{{PatientName}}"] == graph.Patient.User.FullName &&
                values["{{DoctorName}}"] == graph.Doctor.User.FullName &&
                values["{{AppointmentDate}}"].Contains("02/08/2026"))), Times.Once);
    }

    [Fact]
    public async Task SendAppointmentInvitationAsync_WhenSmtpFails_TracksFailureAndReturnsFalse()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 3);
        var setup = CreateService(context, temp.Path);
        setup.Email
            .Setup(service => service.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP offline"));

        var result = await setup.Service.SendAppointmentInvitationAsync(
            graph.Alert.Id,
            graph.Doctor.Id);

        Assert.False(result);
        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)2, notification.Status);
        Assert.False(notification.IsSent);
        Assert.Contains("SMTP offline", notification.ErrorMessage);
    }

    [Fact]
    public async Task SendHealthWarningAsync_WhenRiskIsBelowThreshold_DoesNothing()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 1);
        var setup = CreateService(context, temp.Path);

        var result = await setup.Service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id);

        Assert.False(result);
        Assert.Empty(context.EmailNotifications);
        setup.Email.Verify(service => service.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendHealthWarningAsync_ForRiskLevelTwo_SendsPatientAndActiveFamilyOnly()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 2);
        graph.Prediction.RiskScore = 0.40m;
        context.EmergencyContacts.AddRange(
            new EmergencyContact
            {
                Id = 21,
                PatientId = graph.Patient.Id,
                Patient = graph.Patient,
                FullName = "Family Active",
                Relationship = "Sibling",
                Email = "family@example.com",
                IsActive = true
            },
            new EmergencyContact
            {
                Id = 22,
                PatientId = graph.Patient.Id,
                Patient = graph.Patient,
                FullName = "Family Inactive",
                Relationship = "Sibling",
                Email = "inactive@example.com",
                IsActive = false
            },
            new EmergencyContact
            {
                Id = 23,
                PatientId = graph.Patient.Id,
                Patient = graph.Patient,
                FullName = "Family Deleted",
                Relationship = "Sibling",
                Email = "deleted@example.com",
                IsActive = true,
                IsDeleted = true
            },
            new EmergencyContact
            {
                Id = 24,
                PatientId = graph.Patient.Id,
                Patient = graph.Patient,
                FullName = "Duplicate Patient Email",
                Relationship = "Sibling",
                Email = " PATIENT@example.com ",
                IsActive = true
            });
        await context.SaveChangesAsync();
        var setup = CreateService(context, temp.Path);

        var result = await setup.Service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id);

        Assert.True(result);
        var notifications = await context.EmailNotifications
            .OrderBy(item => item.ToEmail)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, item =>
        {
            Assert.True(item.IsSent);
            Assert.Equal((byte)1, item.Status);
            Assert.Equal(graph.Alert.Id, item.AlertId);
        });
        Assert.Contains(notifications, item =>
            item.ToEmail == graph.Patient.User.Email);
        Assert.Contains(notifications, item =>
            item.ToEmail == "family@example.com");
        setup.Email.Verify(service => service.SendEmailAsync(
            graph.Patient.User.Email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
        setup.Email.Verify(service => service.SendEmailAsync(
            "family@example.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
        setup.Email.Verify(service => service.SendEmailAsync(
            "inactive@example.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        setup.Email.Verify(service => service.SendEmailAsync(
            "deleted@example.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendHealthWarningAsync_WhenSmtpRecovers_RetriesAndMarksSuccess()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 2);
        var setup = CreateService(context, temp.Path);
        setup.Email
            .SetupSequence(service => service.SendEmailAsync(
                graph.Patient.User.Email,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Temporary SMTP error"))
            .ThrowsAsync(new InvalidOperationException("Temporary SMTP error"))
            .Returns(Task.CompletedTask);

        var result = await setup.Service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id);

        Assert.True(result);
        var notification = await context.EmailNotifications.SingleAsync();
        Assert.True(notification.IsSent);
        Assert.Equal((byte)1, notification.Status);
        Assert.Null(notification.ErrorMessage);
        setup.Email.Verify(service => service.SendEmailAsync(
            graph.Patient.User.Email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task SendHealthWarningAsync_WhenAllRetriesFail_ReturnsFalseAndTracksFailure()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 3);
        var setup = CreateService(context, temp.Path);
        setup.Email
            .Setup(service => service.SendEmailAsync(
                graph.Patient.User.Email,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var result = await setup.Service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id);

        Assert.False(result);
        var notification = await context.EmailNotifications.SingleAsync();
        Assert.False(notification.IsSent);
        Assert.Equal((byte)2, notification.Status);
        Assert.Contains("SMTP unavailable", notification.ErrorMessage);
        Assert.Contains("3 attempts", notification.ErrorMessage);
        setup.Email.Verify(service => service.SendEmailAsync(
            graph.Patient.User.Email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task SendHealthWarningAsync_WhenAlreadySent_DoesNotCreateDuplicate()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var graph = await AddAlertGraphAsync(context, riskLevel: 3);
        var setup = CreateService(context, temp.Path);

        await setup.Service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id);
        await setup.Service.SendHealthWarningAsync(
            graph.Patient.Id,
            graph.Prediction.Id);

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.True(notification.IsSent);
        Assert.Equal((byte)1, notification.Status);
        setup.Email.Verify(service => service.SendEmailAsync(
            graph.Patient.User.Email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendDailyVitalLogReminderAsync_CreatesInternalNotificationWithoutSending()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context);
        var setup = CreateService(context, temp.Path);

        await setup.Service.SendDailyVitalLogReminderAsync(
            patient.Id,
            "26/07/2026 08:00");

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)3, notification.Status);
        Assert.False(notification.IsSent);
        Assert.Null(notification.AlertId);
        setup.Email.Verify(service => service.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendDailyVitalLogReminderAsync_WhenTemplateIsMissing_TracksFailure()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context);
        var setup = CreateService(context, temp.Path, htmlBody: string.Empty);

        await setup.Service.SendDailyVitalLogReminderAsync(patient.Id, "Never");

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)2, notification.Status);
        Assert.False(notification.IsSent);
        Assert.False(string.IsNullOrEmpty(notification.ErrorMessage));
    }

    [Fact]
    public async Task SendDoctorAcceptedCheckInAsync_SendsInlineQrAndTracksHistoryDataUri()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context);
        var doctorUser = EntityFactory.User(20, 1, "Doctor Minh");
        var doctor = EntityFactory.Doctor(2, doctorUser);
        var receptionist = EntityFactory.User(30, 2, "Receptionist");
        context.Users.AddRange(doctorUser, receptionist);
        context.Doctors.Add(doctor);
        context.WaitingPatients.Add(new WaitingPatient
        {
            Id = 5,
            PatientId = patient.Id,
            Patient = patient,
            ReceptionistId = receptionist.Id,
            Receptionist = receptionist,
            DoctorId = doctor.Id,
            Doctor = doctor,
            SequenceNumber = 7,
            Status = 1,
            CreatedAt = SmartHealthMonitoring.Common.AppTime.Now,
            AcceptedAt = new DateTime(2026, 7, 26, 1, 0, 0, DateTimeKind.Local)
        });
        await context.SaveChangesAsync();
        var setup = CreateService(context, temp.Path);

        await setup.Service.SendDoctorAcceptedCheckInAsync(5, doctor.Id);

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)1, notification.Status);
        Assert.Contains("data:image/png;base64", notification.Body);
        setup.Email.Verify(service => service.SendEmailAsync(
            patient.User.Email,
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("cid:qrcheckin")),
            It.Is<IReadOnlyDictionary<string, byte[]>>(images =>
                images.ContainsKey("qrcheckin"))), Times.Once);
        setup.Qr.Verify(service => service.BuildCheckInCode(
            5,
            patient.Id,
            doctor.Id,
            7,
            It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task SendBookingConfirmationCheckInAsync_WhenTemplateIsMissing_TracksFailureWithoutSending()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var appointment = await AddAppointmentAsync(context);
        var setup = CreateService(context, temp.Path, htmlBody: string.Empty);

        await setup.Service.SendBookingConfirmationCheckInAsync(appointment.Id);

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)2, notification.Status);
        Assert.False(notification.IsSent);
        setup.Email.Verify(service => service.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, byte[]>?>()), Times.Never);
    }

    [Fact]
    public async Task SendBookingConfirmationCheckInAsync_WhenSuccessful_SendsInlineQr()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var appointment = await AddAppointmentAsync(context);
        var setup = CreateService(context, temp.Path);

        await setup.Service.SendBookingConfirmationCheckInAsync(appointment.Id);

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)1, notification.Status);
        Assert.True(notification.IsSent);
        setup.Qr.Verify(service => service.BuildAppointmentCheckInCode(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.Slot.SlotStart), Times.Once);
        setup.Email.Verify(service => service.SendEmailAsync(
            appointment.Patient.User.Email,
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("cid:qrcheckin")),
            It.IsAny<IReadOnlyDictionary<string, byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task SendAppointmentReminderAsync_WhenSmtpFails_TracksFailure()
    {
        using var temp = new TempDirectory();
        await using var context = TestContextFactory.Create();
        var appointment = await AddAppointmentAsync(context);
        var setup = CreateService(context, temp.Path);
        setup.Email
            .Setup(service => service.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failed"));

        await setup.Service.SendAppointmentReminderAsync(
            appointment.Id,
            "2 hours");

        var notification = await context.EmailNotifications.SingleAsync();
        Assert.Equal((byte)2, notification.Status);
        Assert.False(notification.IsSent);
        Assert.Contains("SMTP failed", notification.ErrorMessage);
    }

    private static TriggerSetup CreateService(
        SmartHealthMonitoringContext context,
        string rootPath,
        string? htmlBody = null)
    {
        var email = new Mock<IEmailService>();
        email
            .Setup(service => service.GetHtmlContentFromFile(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
            .Returns((string _, Dictionary<string, string> replacements) =>
                htmlBody ?? $"<p>{string.Join("|", replacements.Values)}</p>");
        email
            .Setup(service => service.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        email
            .Setup(service => service.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, byte[]>?>()))
            .Returns(Task.CompletedTask);

        var qr = new Mock<IQrCheckInService>();
        qr
            .Setup(service => service.BuildCheckInCode(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>()))
            .Returns("QUEUE-CHECK-IN");
        qr
            .Setup(service => service.BuildAppointmentCheckInCode(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>()))
            .Returns("APPOINTMENT-CHECK-IN");
        qr
            .Setup(service => service.GeneratePng(
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Returns([1, 2, 3]);
        qr
            .Setup(service => service.GenerateDataUri(
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Returns("data:image/png;base64,AQID");

        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = rootPath,
            WebRootPath = rootPath
        };
        var template = new EmailTemplateService(
            environment,
            NullLogger<EmailTemplateService>.Instance);
        var service = new EmailTriggerService(
            context,
            email.Object,
            template,
            qr.Object);

        return new TriggerSetup(service, email, qr);
    }

    private static async Task<AlertGraph> AddAlertGraphAsync(
        SmartHealthMonitoringContext context,
        byte riskLevel)
    {
        var patient = await AddPatientAsync(context);
        var doctorUser = EntityFactory.User(20, 1, "Doctor Minh");
        var doctor = EntityFactory.Doctor(2, doctorUser);
        var prediction = new AiriskPrediction
        {
            Id = 3,
            PatientId = patient.Id,
            Patient = patient,
            RiskScore = 0.85m,
            RiskLevel = riskLevel,
            ModelVersion = "v1",
            PredictedAt = new DateTime(2026, 7, 26, 1, 0, 0, DateTimeKind.Local)
        };
        var alert = new WarningAlert
        {
            Id = 4,
            PatientId = patient.Id,
            Patient = patient,
            PredictionId = prediction.Id,
            Prediction = prediction,
            Status = 0,
            RowVersion = [1],
            FlaggedAt = prediction.PredictedAt,
            ResolutionNote = "Please return for examination"
        };
        context.Users.Add(doctorUser);
        context.Doctors.Add(doctor);
        context.AiriskPredictions.Add(prediction);
        context.WarningAlerts.Add(alert);
        await context.SaveChangesAsync();
        return new AlertGraph(patient, doctor, prediction, alert);
    }

    private static async Task<Patient> AddPatientAsync(SmartHealthMonitoringContext context)
    {
        var patientUser = EntityFactory.User(
            10,
            0,
            "Patient Lan",
            "patient@example.com");
        var patient = EntityFactory.Patient(1, patientUser);
        context.Users.Add(patientUser);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient;
    }

    private static async Task<Appointment> AddAppointmentAsync(
        SmartHealthMonitoringContext context)
    {
        var patient = await AddPatientAsync(context);
        var doctorUser = EntityFactory.User(20, 1, "Doctor Minh");
        var doctor = EntityFactory.Doctor(2, doctorUser);
        var slot = new AppointmentSlot
        {
            Id = 3,
            DoctorId = doctor.Id,
            Doctor = doctor,
            SlotStart = new DateTime(2026, 8, 1, 9, 0, 0),
            SlotEnd = new DateTime(2026, 8, 1, 9, 30, 0),
            Status = AppointmentSlotStatus.Booked,
            PatientId = patient.Id,
            Patient = patient,
            RowVersion = [1]
        };
        var appointment = new Appointment
        {
            Id = 4,
            SlotId = slot.Id,
            Slot = slot,
            PatientId = patient.Id,
            Patient = patient,
            DoctorId = doctor.Id,
            Doctor = doctor,
            Status = AppointmentStatus.Confirmed
        };
        context.Users.Add(doctorUser);
        context.Doctors.Add(doctor);
        context.AppointmentSlots.Add(slot);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        return appointment;
    }

    private sealed record TriggerSetup(
        EmailTriggerService Service,
        Mock<IEmailService> Email,
        Mock<IQrCheckInService> Qr);

    private sealed record AlertGraph(
        Patient Patient,
        Doctor Doctor,
        AiriskPrediction Prediction,
        WarningAlert Alert);
}

