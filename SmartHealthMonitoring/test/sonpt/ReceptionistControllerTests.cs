using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Controllers;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class ReceptionistControllerTests
{
    [Fact]
    public async Task Index_ForwardsPaginationAndReturnsPendingPayments()
    {
        var setup = CreateController();
        var payments = PaymentPage(page: 2, pageSize: 5);
        setup.Receptionist
            .Setup(service => service.GetPendingPaymentsAsync(2, 5))
            .ReturnsAsync(payments);

        var result = await setup.Controller.Index(page: 2, pageSize: 5);

        Assert.Same(payments, Assert.IsType<ViewResult>(result).Model);
        setup.Receptionist.Verify(service => service.GetPendingPaymentsAsync(2, 5), Times.Once);
    }

    [Fact]
    public async Task PaymentHistory_UsesTodayWhenDatesAreNotSpecified()
    {
        var setup = CreateController();
        var payments = PaymentPage();
        DateTime actualFrom = default;
        DateTime actualTo = default;
        setup.Receptionist
            .Setup(service => service.GetPaidPaymentsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), 3, 20))
            .Callback<DateTime, DateTime, int, int>((from, to, _, _) =>
            {
                actualFrom = from;
                actualTo = to;
            })
            .ReturnsAsync(payments);

        var result = await setup.Controller.PaymentHistory(null, null, page: 3, pageSize: 20);

        Assert.Same(payments, Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(DateTime.Today, actualFrom);
        Assert.Equal(DateTime.Today, actualTo);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), setup.Controller.ViewBag.FromDate);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), setup.Controller.ViewBag.ToDate);
    }

    [Fact]
    public async Task Details_ReturnsPaymentOrNotFound()
    {
        var setup = CreateController();
        var payment = Payment(10);
        setup.Receptionist.Setup(service => service.GetPaymentDetailsAsync(10)).ReturnsAsync(payment);
        setup.Receptionist.Setup(service => service.GetPaymentDetailsAsync(99)).ReturnsAsync((Payment?)null);

        var found = await setup.Controller.Details(10);
        var missing = await setup.Controller.Details(99);

        Assert.Same(payment, Assert.IsType<ViewResult>(found).Model);
        Assert.IsType<NotFoundResult>(missing);
    }

    [Fact]
    public async Task Checkout_ForPendingPayment_SetsBankAndQrInformation()
    {
        var setup = CreateController();
        var payment = Payment(12, status: "Pending", amount: 123_456m);
        setup.Receptionist.Setup(service => service.GetPaymentDetailsAsync(12)).ReturnsAsync(payment);

        var result = await setup.Controller.Checkout(12);

        Assert.Same(payment, Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("MB", setup.Controller.ViewBag.BankId);
        Assert.Equal("1508200456788", setup.Controller.ViewBag.AccountNo);
        Assert.Equal("PHAM THE SON", setup.Controller.ViewBag.AccountName);
        Assert.Equal("THANHTOAN HD00012", setup.Controller.ViewBag.TransferContent);
        Assert.Contains("amount=123456", (string)setup.Controller.ViewBag.VietQrUrl);
        Assert.Contains("THANHTOAN%20HD00012", (string)setup.Controller.ViewBag.VietQrUrl);
    }

    [Fact]
    public async Task Checkout_WhenPaymentIsMissingOrAlreadyProcessed_ReturnsExpectedResponse()
    {
        var setup = CreateController();
        setup.Receptionist.Setup(service => service.GetPaymentDetailsAsync(1)).ReturnsAsync((Payment?)null);
        setup.Receptionist.Setup(service => service.GetPaymentDetailsAsync(2)).ReturnsAsync(Payment(2, status: "Paid"));

        var missing = await setup.Controller.Checkout(1);
        var processed = await setup.Controller.Checkout(2);

        Assert.IsType<NotFoundResult>(missing);
        var redirect = Assert.IsType<RedirectToActionResult>(processed);
        Assert.Equal(nameof(ReceptionistController.Details), redirect.ActionName);
        Assert.Equal(2, redirect.RouteValues!["id"]);
        Assert.NotNull(setup.Controller.TempData["Error"]);
    }

    [Fact]
    public async Task ConfirmCash_AndConfirmPayment_ReturnServiceResultAsJson()
    {
        var setup = CreateController();
        setup.Receptionist
            .Setup(service => service.ConfirmCashAsync(5))
            .ReturnsAsync((true, "Paid"));

        var cash = await setup.Controller.ConfirmCash(5);
        var alias = await setup.Controller.ConfirmPayment(5);

        AssertJsonContains(cash, "\"success\":true", "\"message\":\"Paid\"");
        AssertJsonContains(alias, "\"success\":true", "\"message\":\"Paid\"");
        setup.Receptionist.Verify(service => service.ConfirmCashAsync(5), Times.Exactly(2));
    }

    [Fact]
    public async Task CheckQrPayment_ReturnsPaymentStatusAsJson()
    {
        var setup = CreateController();
        setup.Receptionist
            .Setup(service => service.CheckQrPaymentStatusAsync(5))
            .ReturnsAsync((false, "Pending"));

        var result = await setup.Controller.CheckQrPayment(5);

        AssertJsonContains(result, "\"paid\":false", "\"message\":\"Pending\"");
    }

    [Fact]
    public async Task SepayWebhook_WhenPayloadIsEmpty_DoesNotCallService()
    {
        var setup = CreateController();

        var result = await setup.Controller.SepayWebhook(new SepayWebhookPayload());

        AssertJsonContains(result, "\"success\":false", "Empty payload");
        setup.Receptionist.Verify(
            service => service.ProcessSepayWebhookAsync(It.IsAny<string>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task SepayWebhook_ReturnsSuccessPaymentIdOrFailureMessage()
    {
        var setup = CreateController();
        setup.Receptionist
            .Setup(service => service.ProcessSepayWebhookAsync("THANHTOAN HD00042", 100_000m))
            .ReturnsAsync((true, "Success", (int?)42));
        setup.Receptionist
            .Setup(service => service.ProcessSepayWebhookAsync("unknown", 0m))
            .ReturnsAsync((false, "Not found", (int?)null));

        var success = await setup.Controller.SepayWebhook(new SepayWebhookPayload
        {
            Content = "THANHTOAN HD00042",
            TransferAmount = 100_000m
        });
        var failure = await setup.Controller.SepayWebhook(new SepayWebhookPayload { Content = "unknown" });

        AssertJsonContains(success, "\"success\":true", "\"paymentId\":42");
        AssertJsonContains(failure, "\"success\":false", "\"message\":\"Not found\"");
    }

    [Fact]
    public async Task Patients_ReturnsPagedPatientsAndSearchQuery()
    {
        var setup = CreateController();
        var patients = new PagedResult<Patient>
        {
            Items = [new Patient { Id = 1 }],
            Page = 2,
            PageSize = 5,
            TotalCount = 1
        };
        setup.Receptionist
            .Setup(service => service.GetPatientsAsync("son", 2, 5))
            .ReturnsAsync(patients);

        var result = await setup.Controller.Patients("son", page: 2, pageSize: 5);

        var model = Assert.IsType<ReceptionistPatientListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Same(patients, model.Patients);
        Assert.Equal("son", model.SearchQuery);
    }

    [Fact]
    public async Task PatientDetails_ReturnsPatientOrNotFound()
    {
        var setup = CreateController();
        var patient = new Patient { Id = 3 };
        setup.Receptionist.Setup(service => service.GetPatientDetailsAsync(3)).ReturnsAsync(patient);
        setup.Receptionist.Setup(service => service.GetPatientDetailsAsync(4)).ReturnsAsync((Patient?)null);

        var found = await setup.Controller.PatientDetails(3);
        var missing = await setup.Controller.PatientDetails(4);

        Assert.Same(patient, Assert.IsType<ViewResult>(found).Model);
        Assert.IsType<NotFoundResult>(missing);
    }

    [Fact]
    public void RegisterPatient_Get_ReturnsEmptyRegistrationModel()
    {
        var setup = CreateController();

        var result = setup.Controller.RegisterPatient();

        Assert.IsType<ReceptionistRegisterPatientViewModel>(Assert.IsType<ViewResult>(result).Model);
    }

    [Fact]
    public async Task RegisterPatient_Post_WhenModelStateIsInvalid_ReturnsSameModelWithoutCallingService()
    {
        var setup = CreateController();
        var model = ValidRegistrationModel();
        setup.Controller.ModelState.AddModelError(nameof(model.Email), "Invalid");

        var result = await setup.Controller.RegisterPatient(model);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        setup.Receptionist.Verify(service => service.RegisterPatientAsync(It.IsAny<ReceptionistRegisterPatientViewModel>()), Times.Never);
    }

    [Fact]
    public async Task RegisterPatient_Post_WhenRegistrationSucceeds_RedirectsAndStoresSuccessMessage()
    {
        var setup = CreateController();
        var model = ValidRegistrationModel();
        setup.Receptionist
            .Setup(service => service.RegisterPatientAsync(model))
            .ReturnsAsync((true, "Created"));

        var result = await setup.Controller.RegisterPatient(model);

        Assert.Equal(nameof(ReceptionistController.Patients), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("Created", setup.Controller.TempData["Success"]);
    }

    [Fact]
    public async Task RegisterPatient_Post_WhenRegistrationFails_AddsFieldOrModelError()
    {
        var setup = CreateController();
        var emailModel = ValidRegistrationModel();
        setup.Receptionist
            .Setup(service => service.RegisterPatientAsync(emailModel))
            .ReturnsAsync((false, "Email already exists"));

        var emailResult = await setup.Controller.RegisterPatient(emailModel);

        Assert.Same(emailModel, Assert.IsType<ViewResult>(emailResult).Model);
        Assert.True(setup.Controller.ModelState.ContainsKey("Email"));

        var genericSetup = CreateController();
        var genericModel = ValidRegistrationModel();
        genericSetup.Receptionist
            .Setup(service => service.RegisterPatientAsync(genericModel))
            .ReturnsAsync((false, "Unexpected error"));

        var genericResult = await genericSetup.Controller.RegisterPatient(genericModel);

        Assert.Same(genericModel, Assert.IsType<ViewResult>(genericResult).Model);
        Assert.True(genericSetup.Controller.ModelState.ContainsKey(string.Empty));
    }

    [Fact]
    public async Task AddToWaitingList_WhenReceptionistIsUnknown_RedirectsWithoutCallingService()
    {
        var setup = CreateController(userId: null);

        var result = await setup.Controller.AddToWaitingList(patientId: 1, doctorId: 2, slotId: 3);

        Assert.Equal(nameof(ReceptionistController.Patients), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.NotNull(setup.Controller.TempData["Error"]);
        setup.Receptionist.Verify(
            service => service.AddToWaitingListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task AddToWaitingList_StoresServiceMessageAndRedirects()
    {
        var setup = CreateController(userId: 8);
        setup.Receptionist
            .Setup(service => service.AddToWaitingListAsync(1, 2, 3, 8))
            .ReturnsAsync((true, "Added"));

        var result = await setup.Controller.AddToWaitingList(patientId: 1, doctorId: 2, slotId: 3);

        Assert.Equal(nameof(ReceptionistController.Patients), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("Added", setup.Controller.TempData["Success"]);
    }

    [Fact]
    public async Task GetAvailableDoctors_AndGetDoctorSlots_ReturnJsonData()
    {
        var setup = CreateController();
        setup.Receptionist
            .Setup(service => service.GetAvailableDoctorsAsync())
            .ReturnsAsync(new List<dynamic> { new { doctorId = 1, doctorName = "Dr. A" } });
        setup.Receptionist
            .Setup(service => service.GetDoctorSlotsAsync(5))
            .ReturnsAsync(new List<dynamic> { new { slotId = 9 } });

        var doctors = await setup.Controller.GetAvailableDoctors();
        var slots = await setup.Controller.GetDoctorSlots(5);

        AssertJsonContains(doctors, "\"success\":true", "\"doctorId\":1");
        AssertJsonContains(slots, "\"success\":true", "\"slotId\":9");
    }

    [Fact]
    public async Task PendingAppointments_ReturnsServiceList()
    {
        var setup = CreateController();
        var appointments = new List<Appointment> { new() { Id = 1 } };
        setup.Appointments.Setup(service => service.GetPendingAppointmentsAsync()).ReturnsAsync(appointments);

        var result = await setup.Controller.PendingAppointments();

        Assert.Same(appointments, Assert.IsType<ViewResult>(result).Model);
    }

    [Fact]
    public async Task ApproveBooking_SendsEmailOnSuccessAndStillRedirectsWhenEmailFails()
    {
        var setup = CreateController();
        setup.Appointments.Setup(service => service.ApproveAppointmentBookingAsync(3)).ReturnsAsync(true);
        setup.Email.Setup(service => service.SendBookingConfirmationCheckInAsync(3)).Returns(Task.CompletedTask);

        var result = await setup.Controller.ApproveBooking(3);

        Assert.Equal(nameof(ReceptionistController.PendingAppointments), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.NotNull(setup.Controller.TempData["Success"]);
        setup.Email.Verify(service => service.SendBookingConfirmationCheckInAsync(3), Times.Once);

        var emailFailureSetup = CreateController();
        emailFailureSetup.Appointments.Setup(service => service.ApproveAppointmentBookingAsync(4)).ReturnsAsync(true);
        emailFailureSetup.Email
            .Setup(service => service.SendBookingConfirmationCheckInAsync(4))
            .ThrowsAsync(new InvalidOperationException("SMTP failed"));

        var emailFailureResult = await emailFailureSetup.Controller.ApproveBooking(4);

        Assert.Equal(nameof(ReceptionistController.PendingAppointments), Assert.IsType<RedirectToActionResult>(emailFailureResult).ActionName);
        Assert.NotNull(emailFailureSetup.Controller.TempData["Success"]);
    }

    [Theory]
    [InlineData("ApproveBooking")]
    [InlineData("RejectBooking")]
    [InlineData("ApproveCancellation")]
    [InlineData("RejectCancellation")]
    public async Task BookingDecisionActions_WhenServiceFails_StoreErrorAndRedirect(string action)
    {
        var setup = CreateController();
        IActionResult result;

        switch (action)
        {
            case "ApproveBooking":
                setup.Appointments.Setup(service => service.ApproveAppointmentBookingAsync(1)).ReturnsAsync(false);
                result = await setup.Controller.ApproveBooking(1);
                break;
            case "RejectBooking":
                setup.Appointments.Setup(service => service.RejectAppointmentBookingAsync(1)).ReturnsAsync(false);
                result = await setup.Controller.RejectBooking(1);
                break;
            case "ApproveCancellation":
                setup.Appointments.Setup(service => service.ApproveAppointmentCancellationAsync(1)).ReturnsAsync(false);
                result = await setup.Controller.ApproveCancellation(1);
                break;
            default:
                setup.Appointments.Setup(service => service.RejectAppointmentCancellationAsync(1)).ReturnsAsync(false);
                result = await setup.Controller.RejectCancellation(1);
                break;
        }

        Assert.Equal(nameof(ReceptionistController.PendingAppointments), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.NotNull(setup.Controller.TempData["Error"]);
    }

    [Theory]
    [InlineData("RejectBooking")]
    [InlineData("ApproveCancellation")]
    [InlineData("RejectCancellation")]
    public async Task RemainingBookingDecisionActions_WhenServiceSucceeds_StoreSuccessAndRedirect(string action)
    {
        var setup = CreateController();
        IActionResult result;

        switch (action)
        {
            case "RejectBooking":
                setup.Appointments.Setup(service => service.RejectAppointmentBookingAsync(1)).ReturnsAsync(true);
                result = await setup.Controller.RejectBooking(1);
                break;
            case "ApproveCancellation":
                setup.Appointments.Setup(service => service.ApproveAppointmentCancellationAsync(1)).ReturnsAsync(true);
                result = await setup.Controller.ApproveCancellation(1);
                break;
            default:
                setup.Appointments.Setup(service => service.RejectAppointmentCancellationAsync(1)).ReturnsAsync(true);
                result = await setup.Controller.RejectCancellation(1);
                break;
        }

        Assert.Equal(nameof(ReceptionistController.PendingAppointments), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.NotNull(setup.Controller.TempData["Success"]);
    }

    private static ReceptionistControllerSetup CreateController(int? userId = 7)
    {
        var receptionist = new Mock<IReceptionistService>();
        var appointments = new Mock<IAppointmentService>();
        var email = new Mock<IEmailTriggerService>();
        var controller = new ReceptionistController(receptionist.Object, appointments.Object, email.Object)
            .WithUser(userId, roles: ["2"]);
        return new ReceptionistControllerSetup(controller, receptionist, appointments, email);
    }

    private static PagedResult<Payment> PaymentPage(int page = 1, int pageSize = 10)
    {
        return new PagedResult<Payment>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        };
    }

    private static Payment Payment(int id, string status = "Pending", decimal amount = 100_000m)
    {
        return new Payment
        {
            Id = id,
            Status = status,
            TotalAmount = amount
        };
    }

    private static ReceptionistRegisterPatientViewModel ValidRegistrationModel()
    {
        return new ReceptionistRegisterPatientViewModel
        {
            FullName = "Nguyen Van A",
            Email = "a@example.com",
            Phone = "0901234567",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Sex = 0,
            CitizenId = "123456789"
        };
    }

    private static void AssertJsonContains(IActionResult result, params string[] expectedParts)
    {
        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value);
        foreach (var expectedPart in expectedParts)
        {
            Assert.Contains(expectedPart, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record ReceptionistControllerSetup(
        ReceptionistController Controller,
        Mock<IReceptionistService> Receptionist,
        Mock<IAppointmentService> Appointments,
        Mock<IEmailTriggerService> Email);
}
