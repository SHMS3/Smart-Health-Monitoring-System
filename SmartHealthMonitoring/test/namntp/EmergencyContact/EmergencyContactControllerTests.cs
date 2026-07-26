using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Controllers;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class EmergencyContactControllerTests
{
    [Fact]
    public async Task Index_WhenPatientProfileDoesNotExist_RedirectsToProfile()
    {
        await using var context = TestContextFactory.Create();
        var controller = new EmergencyContactController(context).WithUser(99, roles: ["0"]);

        var result = await controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Profile", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Index_ReturnsOwnedContactsInPriorityOrderAndLoadsEditForm()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        context.EmergencyContacts.AddRange(
            Contact(1, patient.Id, "Zulu", isPrimary: false, isActive: false),
            Contact(2, patient.Id, "Alpha", isPrimary: false, isActive: true),
            Contact(3, patient.Id, "Primary", isPrimary: true, isActive: true),
            Contact(4, patient.Id, "Deleted", isDeleted: true));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);

        var result = await controller.Index(editId: 2);

        var model = Assert.IsType<EmergencyContactIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([3, 2, 1], model.Contacts.Select(contact => contact.Id));
        Assert.Equal(2, model.Form.Id);
        Assert.Equal("Alpha", model.Form.FullName);
    }

    [Fact]
    public async Task Save_WhenModelStateIsInvalid_ReturnsIndexWithExistingContacts()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        context.EmergencyContacts.Add(Contact(1, patient.Id, "Existing"));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);
        controller.ModelState.AddModelError("Form.FullName", "Invalid");
        var form = ValidForm();

        var result = await controller.Save(form);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        var model = Assert.IsType<EmergencyContactIndexViewModel>(view.Model);
        Assert.Single(model.Contacts);
        Assert.Same(form, model.Form);
    }

    [Fact]
    public async Task Save_NewPrimaryContact_NormalizesValuesAndClearsPreviousPrimary()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        context.EmergencyContacts.Add(
            Contact(1, patient.Id, "Old Primary", isPrimary: true));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);
        var form = ValidForm();
        form.FullName = "  Nguyen Van A  ";
        form.Relationship = "  Brother  ";
        form.Email = "  PERSON@EXAMPLE.COM ";
        form.Phone = "+84901234567";
        form.IsPrimary = true;

        var result = await controller.Save(form);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await context.EmergencyContacts.SingleAsync(contact => contact.Id != 1);
        Assert.Equal("Nguyen Van A", saved.FullName);
        Assert.Equal("Brother", saved.Relationship);
        Assert.Equal("person@example.com", saved.Email);
        Assert.Equal("0901234567", saved.Phone);
        Assert.True(saved.IsPrimary);
        Assert.False((await context.EmergencyContacts.FindAsync(1))!.IsPrimary);
    }

    [Fact]
    public async Task Save_WhenContactMethodIsDuplicated_ReturnsValidationErrors()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        context.EmergencyContacts.Add(new EmergencyContact
        {
            Id = 1,
            PatientId = patient.Id,
            FullName = "Existing",
            Relationship = "Parent",
            Email = "same@example.com",
            Phone = "0901234567",
            IsActive = true
        });
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);
        var form = ValidForm();
        form.Email = " SAME@EXAMPLE.COM ";
        form.Phone = "+84901234567";

        var result = await controller.Save(form);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Form.Email"));
        Assert.True(controller.ModelState.ContainsKey("Form.Phone"));
        Assert.Equal(1, await context.EmergencyContacts.CountAsync());
    }

    [Fact]
    public async Task Save_WhenEditingAnotherPatientsContact_DoesNotModifyIt()
    {
        await using var context = TestContextFactory.Create();
        await AddPatientAsync(context, 1, 10);
        var otherPatient = await AddPatientAsync(context, 2, 20);
        context.EmergencyContacts.Add(Contact(7, otherPatient.Id, "Other"));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);
        var form = ValidForm();
        form.Id = 7;
        form.FullName = "Hacked";

        var result = await controller.Save(form);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Other", (await context.EmergencyContacts.FindAsync(7))!.FullName);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task SetPrimary_ActivatesSelectedAndClearsOtherPrimaryContact()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        context.EmergencyContacts.AddRange(
            Contact(1, patient.Id, "Old", isPrimary: true),
            Contact(2, patient.Id, "New", isPrimary: false, isActive: false));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);

        await controller.SetPrimary(2);

        Assert.False((await context.EmergencyContacts.FindAsync(1))!.IsPrimary);
        var selected = (await context.EmergencyContacts.FindAsync(2))!;
        Assert.True(selected.IsPrimary);
        Assert.True(selected.IsActive);
    }

    [Fact]
    public async Task ToggleActive_WhenDisablingPrimaryContact_AlsoClearsPrimaryFlag()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        context.EmergencyContacts.Add(
            Contact(1, patient.Id, "Primary", isPrimary: true, isActive: true));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);

        await controller.ToggleActive(1);

        var contact = (await context.EmergencyContacts.FindAsync(1))!;
        Assert.False(contact.IsActive);
        Assert.False(contact.IsPrimary);
    }

    [Fact]
    public async Task Delete_SoftDeletesOnlyOwnedContact()
    {
        await using var context = TestContextFactory.Create();
        var patient = await AddPatientAsync(context, 1, 10);
        var otherPatient = await AddPatientAsync(context, 2, 20);
        context.EmergencyContacts.AddRange(
            Contact(1, patient.Id, "Owned", isPrimary: true),
            Contact(2, otherPatient.Id, "Other"));
        await context.SaveChangesAsync();
        var controller = new EmergencyContactController(context).WithUser(10, roles: ["0"]);

        await controller.Delete(1);
        await controller.Delete(2);

        var owned = (await context.EmergencyContacts.FindAsync(1))!;
        Assert.True(owned.IsDeleted);
        Assert.False(owned.IsActive);
        Assert.False(owned.IsPrimary);
        Assert.False((await context.EmergencyContacts.FindAsync(2))!.IsDeleted);
    }

    private static EmergencyContactFormViewModel ValidForm()
    {
        return new EmergencyContactFormViewModel
        {
            FullName = "Nguyen Van A",
            Relationship = "Brother",
            Email = "person@example.com",
            IsActive = true
        };
    }

    private static EmergencyContact Contact(
        int id,
        int patientId,
        string name,
        bool isPrimary = false,
        bool isActive = true,
        bool isDeleted = false)
    {
        return new EmergencyContact
        {
            Id = id,
            PatientId = patientId,
            FullName = name,
            Relationship = "Family",
            Email = $"{name.Replace(" ", string.Empty)}@example.com",
            IsPrimary = isPrimary,
            IsActive = isActive,
            IsDeleted = isDeleted
        };
    }

    private static async Task<Patient> AddPatientAsync(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context,
        int patientId,
        int userId)
    {
        var user = EntityFactory.User(userId, 0);
        var patient = EntityFactory.Patient(patientId, user);
        context.Users.Add(user);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient;
    }
}
