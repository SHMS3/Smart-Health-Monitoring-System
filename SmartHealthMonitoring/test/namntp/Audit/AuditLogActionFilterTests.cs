using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHealthMonitoring.Filters;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.UnitTests;

public class AuditLogActionFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_ForSuccessfulAdminPost_LogsNormalizedAction()
    {
        await using var db = TestContextFactory.Create();
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
        var filter = new AuditLogActionFilter(
            db,
            audit.Object,
            NullLogger<AuditLogActionFilter>.Instance);
        var contexts = CreateContexts(
            "POST",
            roles: ["2"],
            controller: "Patient",
            action: "UpdateProfile",
            arguments: new Dictionary<string, object?> { ["patientId"] = 15 });

        await filter.OnActionExecutionAsync(
            contexts.Executing,
            () => Task.FromResult(contexts.Executed));

        audit.Verify(service => service.LogAsync(
            "Update",
            "Patient",
            "15",
            It.Is<string>(description =>
                description.Contains("UpdateProfile") &&
                description.Contains("Patient")),
            null,
            null), Times.Once);
    }

    [Theory]
    [InlineData("GET", true, "2", true, 200)]
    [InlineData("POST", false, "2", true, 200)]
    [InlineData("POST", true, "0", true, 200)]
    [InlineData("POST", true, "2", false, 200)]
    [InlineData("POST", true, "2", true, 400)]
    public async Task OnActionExecutionAsync_WhenRequestIsNotAuditable_DoesNotLog(
        string method,
        bool authenticated,
        string role,
        bool modelStateValid,
        int statusCode)
    {
        await using var db = TestContextFactory.Create();
        var audit = new Mock<IAuditLogService>();
        var filter = new AuditLogActionFilter(
            db,
            audit.Object,
            NullLogger<AuditLogActionFilter>.Instance);
        var contexts = CreateContexts(
            method,
            authenticated ? [role] : [],
            result: new StatusCodeResult(statusCode));
        if (!modelStateValid)
        {
            contexts.Executing.ModelState.AddModelError("Name", "Invalid");
        }

        await filter.OnActionExecutionAsync(
            contexts.Executing,
            () => Task.FromResult(contexts.Executed));

        audit.Verify(
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
    public async Task OnActionExecutionAsync_WhenActionThrowsUnhandledException_DoesNotLog()
    {
        await using var db = TestContextFactory.Create();
        var audit = new Mock<IAuditLogService>();
        var filter = new AuditLogActionFilter(
            db,
            audit.Object,
            NullLogger<AuditLogActionFilter>.Instance);
        var contexts = CreateContexts("DELETE", ["1"]);
        contexts.Executed.Exception = new InvalidOperationException("business failure");

        await filter.OnActionExecutionAsync(
            contexts.Executing,
            () => Task.FromResult(contexts.Executed));

        audit.Verify(
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
    public async Task OnActionExecutionAsync_WhenActionAlreadyCreatedAuditLog_DoesNotDuplicate()
    {
        await using var db = TestContextFactory.Create();
        db.AuditLogs.Add(new AuditLog
        {
            Action = "Update",
            EntityName = "Patient",
            Description = "Explicit audit"
        });
        var audit = new Mock<IAuditLogService>();
        var filter = new AuditLogActionFilter(
            db,
            audit.Object,
            NullLogger<AuditLogActionFilter>.Instance);
        var contexts = CreateContexts("POST", ["2"]);

        await filter.OnActionExecutionAsync(
            contexts.Executing,
            () => Task.FromResult(contexts.Executed));

        audit.Verify(
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
    public async Task OnActionExecutionAsync_WhenAuditServiceFails_DoesNotBreakAction()
    {
        await using var db = TestContextFactory.Create();
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(service => service.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var filter = new AuditLogActionFilter(
            db,
            audit.Object,
            NullLogger<AuditLogActionFilter>.Instance);
        var contexts = CreateContexts("PATCH", ["1"]);

        var exception = await Record.ExceptionAsync(() =>
            filter.OnActionExecutionAsync(
                contexts.Executing,
                () => Task.FromResult(contexts.Executed)));

        Assert.Null(exception);
    }

    private static FilterContexts CreateContexts(
        string method,
        string[]? roles = null,
        string controller = "AdminSettings",
        string action = "Save",
        Dictionary<string, object?>? arguments = null,
        IActionResult? result = null)
    {
        var claims = (roles ?? ["2"])
            .Select(role => new Claim(ClaimTypes.Role, role))
            .ToList();
        var identity = new ClaimsIdentity(claims, "UnitTest");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        httpContext.Request.Method = method;

        var actionDescriptor = new ControllerActionDescriptor
        {
            RouteValues = new Dictionary<string, string?>
            {
                ["controller"] = controller,
                ["action"] = action
            }
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor,
            new ModelStateDictionary());
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(
            actionContext,
            filters,
            arguments ?? new Dictionary<string, object?>(),
            new object());
        var executed = new ActionExecutedContext(actionContext, filters, new object())
        {
            Result = result
        };

        return new FilterContexts(executing, executed);
    }

    private sealed record FilterContexts(
        ActionExecutingContext Executing,
        ActionExecutedContext Executed);
}
