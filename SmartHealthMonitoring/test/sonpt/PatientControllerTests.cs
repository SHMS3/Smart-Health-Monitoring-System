using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Controllers;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class PatientControllerTests
{
    [Fact]
    public async Task Index_WithoutDateFilter_ReturnsTodaysHistoryAndAllowsLogging()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.AddRange(
            DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today.AddHours(8)),
            DailyVitalLogTestSetup.Log(2, patient.Id, DateTime.Today.AddDays(-1)));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);

        var result = await controller.Index(fromDate: null, toDate: null);

        var model = Assert.IsType<PagedResult<DailyVitalLogViewModel>>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([1], model.Items.Select(log => log.Id));
        Assert.True((bool)controller.ViewBag.CanLog);
        Assert.Null(controller.ViewBag.FromDate);
        Assert.Null(controller.ViewBag.ToDate);
    }

    [Fact]
    public async Task Index_WhenDailyLimitIsReached_DisablesLogging()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.AddRange(Enumerable.Range(1, 10).Select(id =>
            DailyVitalLogTestSetup.Log(id, patient.Id, DateTime.Today.AddMinutes(id))));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);

        var result = await controller.Index(null, null);

        Assert.IsType<ViewResult>(result);
        Assert.False((bool)controller.ViewBag.CanLog);
        Assert.NotNull(controller.ViewBag.LogMessage);
    }

    [Fact]
    public async Task Index_WhenLastLogIsLessThanOneHourOld_DisablesLoggingUntilNextWindow()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        var loggedAt = DateTime.Now.AddMinutes(-5);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, loggedAt));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);

        var result = await controller.Index(null, null);

        Assert.IsType<ViewResult>(result);
        Assert.False((bool)controller.ViewBag.CanLog);
        Assert.Equal(loggedAt.AddHours(1), (DateTime?)controller.ViewBag.NextLogTime);
        Assert.True((int)controller.ViewBag.RemainingSeconds > 0);
    }

    [Fact]
    public async Task Index_WhenDateFilterIsInvalid_ReturnsEmptyHistoryAndError()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        var controller = CreateController(context, patient.UserId);

        var result = await controller.Index(DateTime.Today, DateTime.Today.AddDays(-1));

        var model = Assert.IsType<PagedResult<DailyVitalLogViewModel>>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Items);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public void Create_Get_ReturnsView()
    {
        using var context = TestContextFactory.Create();
        var controller = CreateController(context, userId: 10);

        var result = controller.Create();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Create_Post_WhenModelStateIsInvalid_ReturnsSameModel()
    {
        await using var context = TestContextFactory.Create();
        var controller = CreateController(context, userId: 10);
        var model = DailyVitalLogTestSetup.ValidModel();
        controller.ModelState.AddModelError(nameof(model.SystolicBp), "Required");

        var result = await controller.Create(model);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(context.DailyVitalLogs);
    }

    [Fact]
    public async Task Create_Post_WhenValid_CreatesLogAndRedirectsToHistory()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        var controller = CreateController(context, patient.UserId);

        var result = await controller.Create(DailyVitalLogTestSetup.ValidModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PatientController.Index), redirect.ActionName);
        Assert.Single(context.DailyVitalLogs);
    }

    [Fact]
    public async Task Create_Post_WhenServiceFails_AddsErrorAndReturnsModel()
    {
        await using var context = TestContextFactory.Create();
        var controller = CreateController(context, userId: 10);
        var model = DailyVitalLogTestSetup.ValidModel();

        var result = await controller.Create(model);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(context.DailyVitalLogs);
    }

    [Fact]
    public async Task Details_ReturnsLogDetailsOrRedirectsWhenLogIsMissing()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);

        var found = await controller.Details(1);
        var missing = await controller.Details(999);

        Assert.Equal(1, Assert.IsType<DailyVitalLogViewModel>(Assert.IsType<ViewResult>(found).Model).Id);
        Assert.Equal(nameof(PatientController.Index), Assert.IsType<RedirectToActionResult>(missing).ActionName);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Update_Get_ReturnsEditableLogOrRedirectsWhenMissing()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today, systolicBp: 125));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);

        var found = await controller.Update(1);
        var missing = await controller.Update(999);

        Assert.Equal((short)125, Assert.IsType<DailyVitalLogViewModel>(Assert.IsType<ViewResult>(found).Model).SystolicBp);
        Assert.Equal(nameof(PatientController.Index), Assert.IsType<RedirectToActionResult>(missing).ActionName);
    }

    [Fact]
    public async Task Update_Post_HandlesIdMismatchInvalidModelAndSuccessfulUpdate()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today));
        await context.SaveChangesAsync();

        var mismatchController = CreateController(context, patient.UserId);
        var mismatch = await mismatchController.Update(2, new DailyVitalLogViewModel { Id = 1 });
        Assert.IsType<BadRequestResult>(mismatch);

        var invalidController = CreateController(context, patient.UserId);
        var invalidModel = DailyVitalLogTestSetup.ValidModel();
        invalidModel.Id = 1;
        invalidController.ModelState.AddModelError(nameof(invalidModel.HeartRate), "Required");
        var invalid = await invalidController.Update(1, invalidModel);
        Assert.Same(invalidModel, Assert.IsType<ViewResult>(invalid).Model);

        context.ChangeTracker.Clear();
        var successController = CreateController(context, patient.UserId);
        var validModel = DailyVitalLogTestSetup.ValidModel(systolicBp: 135);
        validModel.Id = 1;
        var success = await successController.Update(1, validModel);

        var redirect = Assert.IsType<RedirectToActionResult>(success);
        Assert.Equal(nameof(PatientController.Details), redirect.ActionName);
        Assert.Equal((short)135, (await context.DailyVitalLogs.SingleAsync()).SystolicBp);
        Assert.NotNull(successController.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Update_Post_WhenLogIsLocked_RedirectsToDetailsWithError()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today, isUpdateLocked: true));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);
        var model = DailyVitalLogTestSetup.ValidModel();
        model.Id = 1;

        var result = await controller.Update(1, model);

        Assert.Equal(nameof(PatientController.Details), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Tracker_ReturnsTrendOrRedirectsWhenPatientDoesNotExist()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today.AddHours(8)));
        await context.SaveChangesAsync();
        var controller = CreateController(context, patient.UserId);

        var success = await controller.Tracker(days: 1);

        Assert.Equal(1, Assert.IsType<PersonalHealthTrackerViewModel>(Assert.IsType<ViewResult>(success).Model).Days);

        var missingPatientController = CreateController(context, userId: 999);
        var failure = await missingPatientController.Tracker();
        Assert.Equal(nameof(PatientController.Index), Assert.IsType<RedirectToActionResult>(failure).ActionName);
        Assert.NotNull(missingPatientController.TempData["ErrorMessage"]);
    }

    private static PatientController CreateController(SmartHealthMonitoringContext context, int userId)
    {
        return new PatientController(DailyVitalLogTestSetup.CreateService(context))
            .WithUser(userId, roles: ["0"]);
    }
}
