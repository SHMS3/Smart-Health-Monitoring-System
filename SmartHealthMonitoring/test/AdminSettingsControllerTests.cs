using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartHealthMonitoring.Controllers.Admin;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class AdminSettingsControllerTests
{
    [Fact]
    public async Task Index_WhenCurrentAdminDoesNotExist_RedirectsToLogin()
    {
        await using var context = TestContextFactory.Create();
        var setup = CreateController(context, userId: 99);

        var result = await setup.Controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Auth", redirect.ControllerName);
    }

    [Fact]
    public async Task Index_ReturnsCurrentAdminProfileAndRequestedSection()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, "Admin Nam", "admin@example.com");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);

        var result = await setup.Controller.Index("security");

        var model = Assert.IsType<AdminSettingsViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("security", model.ActiveSection);
        Assert.Equal(admin.Id, model.Profile.UserId);
        Assert.Equal("Admin Nam", model.Profile.FullName);
        Assert.False(model.IsGoogleAccount);
    }

    [Fact]
    public async Task UpdateProfile_WhenEmailIsUsedByAnotherUser_ReturnsValidationView()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, "Admin Nam", "admin@example.com");
        context.Users.AddRange(
            admin,
            EntityFactory.User(2, 1, email: "used@example.com"));
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);
        var profile = new AdminProfileSettingsViewModel
        {
            FullName = " Updated ",
            Email = " used@example.com "
        };

        var result = await setup.Controller.UpdateProfile(profile);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        Assert.False(setup.Controller.ModelState.IsValid);
        Assert.Equal(admin.Id, profile.UserId);
        Assert.Equal("admin@example.com", (await context.Users.FindAsync(admin.Id))!.Email);
    }

    [Fact]
    public async Task UpdateProfile_WhenNothingChanged_DoesNotWriteAudit()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, "Admin Nam", "admin@example.com");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);
        var profile = new AdminProfileSettingsViewModel
        {
            FullName = " Admin Nam ",
            Email = " admin@example.com "
        };

        var result = await setup.Controller.UpdateProfile(profile);

        Assert.IsType<RedirectToActionResult>(result);
        setup.Audit.Verify(
            service => service.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WhenChanged_UpdatesIdentityAndWritesAudit()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, "Old Admin", "old@example.com");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);
        var profile = new AdminProfileSettingsViewModel
        {
            FullName = " New Admin ",
            Email = " new@example.com "
        };

        var result = await setup.Controller.UpdateProfile(profile);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = (await context.Users.FindAsync(admin.Id))!;
        Assert.Equal("New Admin", saved.FullName);
        Assert.Equal("new@example.com", saved.Email);
        setup.Authentication.Verify(service => service.SignInAsync(
            It.IsAny<HttpContext>(),
            CookieAuthenticationDefaults.AuthenticationScheme,
            It.Is<System.Security.Claims.ClaimsPrincipal>(principal =>
                principal.FindFirst("FullName")!.Value == "New Admin"),
            It.IsAny<AuthenticationProperties>()), Times.Once);
        setup.Audit.Verify(service => service.LogAsync(
            "Update",
            "AdminSettings",
            admin.Id.ToString(),
            It.IsAny<string>(),
            admin.Id,
            "New Admin"), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_ForGoogleAccount_ReturnsValidationError()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, passwordHash: string.Empty);
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);

        var result = await setup.Controller.ChangePassword(Password("old", "new-password"));

        Assert.IsType<ViewResult>(result);
        Assert.False(setup.Controller.ModelState.IsValid);
        Assert.True(setup.Controller.ModelState.ContainsKey("Password.CurrentPassword"));
    }

    [Fact]
    public async Task ChangePassword_WhenCurrentPasswordIsWrong_ReturnsValidationError()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, passwordHash: "correct-password");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);

        var result = await setup.Controller.ChangePassword(
            Password("wrong-password", "new-password"));

        Assert.IsType<ViewResult>(result);
        Assert.True(setup.Controller.ModelState.ContainsKey("Password.CurrentPassword"));
        Assert.Equal("correct-password", (await context.Users.FindAsync(1))!.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_WhenNewPasswordMatchesCurrent_ReturnsValidationError()
    {
        await using var context = TestContextFactory.Create();
        var hash = BCrypt.Net.BCrypt.HashPassword("same-password");
        var admin = EntityFactory.User(1, 2, passwordHash: hash);
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);

        var result = await setup.Controller.ChangePassword(
            Password("same-password", "same-password"));

        Assert.IsType<ViewResult>(result);
        Assert.True(setup.Controller.ModelState.ContainsKey("Password.NewPassword"));
    }

    [Fact]
    public async Task ChangePassword_WithValidPasswords_HashesNewPasswordAndAudits()
    {
        await using var context = TestContextFactory.Create();
        var admin = EntityFactory.User(1, 2, "Admin Nam", passwordHash: "old-password");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var setup = CreateController(context, admin.Id);

        var result = await setup.Controller.ChangePassword(
            Password("old-password", "new-password"));

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("security", redirect.RouteValues!["section"]);
        var savedHash = (await context.Users.FindAsync(1))!.PasswordHash;
        Assert.True(BCrypt.Net.BCrypt.Verify("new-password", savedHash));
        setup.Audit.Verify(service => service.LogAsync(
            "ChangePassword",
            "AdminSettings",
            admin.Id.ToString(),
            It.IsAny<string>(),
            admin.Id,
            admin.FullName), Times.Once);
    }

    private static ChangePasswordViewModel Password(string current, string next)
    {
        return new ChangePasswordViewModel
        {
            CurrentPassword = current,
            NewPassword = next,
            ConfirmNewPassword = next
        };
    }

    private static ControllerSetup CreateController(
        SmartHealthMonitoring.Context.SmartHealthMonitoringContext context,
        int userId)
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

        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.AuthenticateAsync(
                It.IsAny<HttpContext>(),
                CookieAuthenticationDefaults.AuthenticationScheme))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(
                new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity("UnitTest")),
                new AuthenticationProperties { IsPersistent = true },
                CookieAuthenticationDefaults.AuthenticationScheme)));
        authentication
            .Setup(service => service.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string?>(),
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties?>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection()
            .AddSingleton(authentication.Object)
            .BuildServiceProvider();
        var controller = new AdminSettingsController(context, audit.Object)
            .WithUser(userId, services: services, roles: ["2"]);

        return new ControllerSetup(controller, audit, authentication);
    }

    private sealed record ControllerSetup(
        AdminSettingsController Controller,
        Mock<IAuditLogService> Audit,
        Mock<IAuthenticationService> Authentication);
}
