using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Controllers;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class EmailNotificationControllerTests
{
    [Fact]
    public async Task Index_ForAdmin_AppliesFiltersAndBuildsStatsAndOptions()
    {
        await using var context = TestContextFactory.Create();
        var patientUser = EntityFactory.User(10, 0, "Patient Lan", "lan@example.com");
        var patient = EntityFactory.Patient(1, patientUser);
        var doctorUser = EntityFactory.User(20, 1, "Doctor Minh", "minh@example.com");
        var doctor = EntityFactory.Doctor(2, doctorUser);
        context.Users.AddRange(patientUser, doctorUser);
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        var today = DateTime.Today;
        context.EmailNotifications.AddRange(
            Notification(1, patient, today.AddHours(8), 1, doctor.Id, "Follow up"),
            Notification(2, patient, today.AddHours(9), 2, null, "Warning", "5.4.5 limit"),
            Notification(3, patient, today.AddDays(-8), 1, null, "Old"));
        await context.SaveChangesAsync();
        var controller = new EmailNotificationController(context).WithUser(99, roles: ["2"]);

        var result = await controller.Index(
            status: 1,
            emailType: null,
            fromDate: today,
            toDate: today,
            keyword: "lan@example.com",
            patientId: patient.Id,
            sender: $"doctor:{doctor.Id}",
            page: 1);

        var model = Assert.IsType<EmailHistoryIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        var email = Assert.Single(model.Emails);
        Assert.Equal(1, email.Id);
        Assert.Equal("Patient Lan", email.PatientName);
        Assert.Equal("Doctor Minh", email.SenderName);
        Assert.NotEmpty(email.StatusDisplay);
        Assert.Equal(2, model.Stats.TotalLast7Days);
        Assert.Equal(1, model.Stats.Succeeded);
        Assert.Equal(1, model.Stats.Failed);
        Assert.Equal(1, model.Stats.ByAI);
        Assert.Equal(1, model.Stats.ByDoctor);
        Assert.Contains(model.PatientOptions, option => option.Value == patient.Id.ToString());
        Assert.Contains(model.SenderOptions, option => option.Value == "system");
        Assert.Contains(model.SenderOptions, option => option.Value == $"doctor:{doctor.Id}");
    }

    [Fact]
    public async Task Index_ForDoctor_ReturnsOnlySystemAndOwnEmails()
    {
        await using var context = TestContextFactory.Create();
        var patientUser = EntityFactory.User(10, 0);
        var patient = EntityFactory.Patient(1, patientUser);
        var currentDoctorUser = EntityFactory.User(20, 1);
        var currentDoctor = EntityFactory.Doctor(2, currentDoctorUser);
        var otherDoctorUser = EntityFactory.User(30, 1);
        var otherDoctor = EntityFactory.Doctor(3, otherDoctorUser);
        context.Users.AddRange(patientUser, currentDoctorUser, otherDoctorUser);
        context.Patients.Add(patient);
        context.Doctors.AddRange(currentDoctor, otherDoctor);
        var today = DateTime.Today;
        context.EmailNotifications.AddRange(
            Notification(1, patient, today.AddHours(8), 1, null, "System"),
            Notification(2, patient, today.AddHours(9), 1, currentDoctor.Id, "Own"),
            Notification(3, patient, today.AddHours(10), 1, otherDoctor.Id, "Other"));
        await context.SaveChangesAsync();
        var controller = new EmailNotificationController(context)
            .WithUser(currentDoctorUser.Id, roles: ["1"]);

        var result = await controller.Index(
            null,
            null,
            today,
            today,
            null,
            null,
            null,
            1);

        var model = Assert.IsType<EmailHistoryIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([2, 1], model.Emails.Select(email => email.Id));
        Assert.Equal(2, model.Stats.TotalLast7Days);
        Assert.DoesNotContain(
            model.SenderOptions,
            option => option.Value == $"doctor:{otherDoctor.Id}");
    }

    [Fact]
    public async Task Index_ForDoctorWithoutProfile_ReturnsForbid()
    {
        await using var context = TestContextFactory.Create();
        var controller = new EmailNotificationController(context)
            .WithUser(404, roles: ["1"]);

        var result = await controller.Index(
            null,
            null,
            DateTime.Today,
            DateTime.Today,
            null,
            null,
            null,
            1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_WhenRequestedPageExceedsLastPage_ClampsToLastPage()
    {
        await using var context = TestContextFactory.Create();
        var patientUser = EntityFactory.User(10, 0);
        var patient = EntityFactory.Patient(1, patientUser);
        context.Users.Add(patientUser);
        context.Patients.Add(patient);
        var today = DateTime.Today;
        context.EmailNotifications.AddRange(
            Enumerable.Range(1, 12)
                .Select(id => Notification(
                    id,
                    patient,
                    today.AddMinutes(id),
                    1,
                    null,
                    $"Email {id}")));
        await context.SaveChangesAsync();
        var controller = new EmailNotificationController(context).WithUser(1, roles: ["2"]);

        var result = await controller.Index(
            null,
            null,
            today,
            today,
            null,
            null,
            null,
            99);

        var model = Assert.IsType<EmailHistoryIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.CurrentPage);
        Assert.Equal(2, model.TotalPages);
        Assert.Equal(2, model.Emails.Count);
        Assert.Equal(11, model.StartItem);
        Assert.Equal(12, model.EndItem);
    }

    [Fact]
    public async Task Index_MapsInternalNotificationAndSanitizesRawEmailError()
    {
        await using var context = TestContextFactory.Create();
        var patientUser = EntityFactory.User(10, 0);
        var patient = EntityFactory.Patient(1, patientUser);
        context.Users.Add(patientUser);
        context.Patients.Add(patient);
        var today = DateTime.Today;
        context.EmailNotifications.Add(
            Notification(
                1,
                patient,
                today,
                3,
                null,
                "Internal reminder",
                "Daily user sending limit exceeded: raw smtp error"));
        await context.SaveChangesAsync();
        var controller = new EmailNotificationController(context).WithUser(1, roles: ["2"]);

        var result = await controller.Index(
            3,
            null,
            today,
            today,
            null,
            null,
            "system",
            1);

        var email = Assert.Single(Assert.IsType<EmailHistoryIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model).Emails);
        Assert.NotEqual("Daily user sending limit exceeded: raw smtp error", email.ErrorMessage);
        Assert.NotEmpty(email.ErrorMessage!);
        Assert.NotEqual("Other", email.EmailType);
    }

    private static EmailNotification Notification(
        int id,
        Patient patient,
        DateTime createdAt,
        byte status,
        int? doctorId,
        string subject,
        string? error = null)
    {
        return new EmailNotification
        {
            Id = id,
            PatientId = patient.Id,
            Patient = patient,
            ToEmail = patient.User.Email,
            Subject = subject,
            Body = "<p>Body</p>",
            Status = status,
            IsSent = status == 1,
            SentByDoctorId = doctorId,
            CreatedAt = createdAt,
            ErrorMessage = error
        };
    }
}
