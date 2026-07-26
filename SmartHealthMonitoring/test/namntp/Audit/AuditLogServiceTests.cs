using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHealthMonitoring.Hubs;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.UnitTests;

public class AuditLogServiceTests
{
    [Fact]
    public async Task LogAsync_UsesAuthenticatedRequestMetadata()
    {
        await using var context = TestContextFactory.Create();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim("FullName", "Admin Nam"),
                new Claim(ClaimTypes.Email, "nam@example.com")
            ], "UnitTest"))
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Request.Headers.UserAgent = "Unit Test Browser";
        var hub = CreateHubContext();
        var service = new AuditLogService(
            context,
            new HttpContextAccessor { HttpContext = httpContext },
            hub.Context.Object,
            NullLogger<AuditLogService>.Instance);

        await service.LogAsync(
            "Update",
            "PatientInterface",
            "settings",
            "Updated patient interface",
            7,
            "Patient Seven");

        var log = await context.AuditLogs.SingleAsync();
        Assert.Equal(42, log.ActorUserId);
        Assert.Equal("Admin Nam", log.ActorName);
        Assert.Equal("nam@example.com", log.ActorEmail);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("Unit Test Browser", log.UserAgent);
        Assert.Equal(7, log.TargetUserId);
        Assert.Equal("Patient Seven", log.TargetName);
    }

    [Fact]
    public async Task LogForActorAsync_TruncatesFieldsAndBroadcastsCreatedLog()
    {
        await using var context = TestContextFactory.Create();
        var hub = CreateHubContext();
        var service = new AuditLogService(
            context,
            new HttpContextAccessor(),
            hub.Context.Object,
            NullLogger<AuditLogService>.Instance);
        var before = SmartHealthMonitoring.Common.AppTime.Now;

        await service.LogForActorAsync(
            null,
            null,
            null,
            new string('A', 60),
            new string('E', 110),
            new string('I', 70),
            new string('D', 1100),
            targetName: new string('T', 110),
            ipAddress: new string('1', 50),
            userAgent: new string('U', 520));

        var log = await context.AuditLogs.SingleAsync();
        Assert.Equal("Unknown", log.ActorName);
        Assert.Equal(string.Empty, log.ActorEmail);
        Assert.Equal(50, log.Action.Length);
        Assert.Equal(100, log.EntityName.Length);
        Assert.Equal(64, log.EntityId!.Length);
        Assert.Equal(1000, log.Description.Length);
        Assert.Equal(100, log.TargetName!.Length);
        Assert.Equal(45, log.IpAddress!.Length);
        Assert.Equal(512, log.UserAgent!.Length);
        Assert.InRange(log.CreatedAt, before, SmartHealthMonitoring.Common.AppTime.Now);

        hub.Client.Verify(
            client => client.SendCoreAsync(
                "AuditLogCreated",
                It.Is<object?[]>(arguments => arguments.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogForActorAsync_WhenBroadcastFails_StillPersistsLog()
    {
        await using var context = TestContextFactory.Create();
        var hub = CreateHubContext();
        hub.Client
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));
        var service = new AuditLogService(
            context,
            new HttpContextAccessor(),
            hub.Context.Object,
            NullLogger<AuditLogService>.Instance);

        await service.LogForActorAsync(
            1,
            "Admin",
            "admin@example.com",
            "Delete",
            "Contact",
            null,
            "Deleted contact");

        Assert.Equal(1, await context.AuditLogs.CountAsync());
    }

    private static HubMocks CreateHubContext()
    {
        var client = new Mock<IClientProxy>();
        client
            .Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients
            .Setup(hubClients => hubClients.Group(AuditLogHub.AdminGroupName))
            .Returns(client.Object);

        var context = new Mock<IHubContext<AuditLogHub>>();
        context.SetupGet(hubContext => hubContext.Clients).Returns(clients.Object);

        return new HubMocks(context, client);
    }

    private sealed record HubMocks(
        Mock<IHubContext<AuditLogHub>> Context,
        Mock<IClientProxy> Client);
}
