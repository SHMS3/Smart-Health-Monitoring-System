using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SmartHealthMonitoring.Hubs;

namespace SmartHealthMonitoring.UnitTests;

public class AuditLogHubTests
{
    [Fact]
    public async Task OnConnectedAsync_ForAdmin_AddsConnectionToAdminGroup()
    {
        var setup = CreateHub("2");

        await setup.Hub.OnConnectedAsync();

        setup.Groups.Verify(groups => groups.AddToGroupAsync(
            "connection-1",
            AuditLogHub.AdminGroupName,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_ForNonAdmin_DoesNotAddConnectionToAdminGroup()
    {
        var setup = CreateHub("1");

        await setup.Hub.OnConnectedAsync();

        setup.Groups.Verify(groups => groups.AddToGroupAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_AlwaysRemovesConnectionFromAdminGroup()
    {
        var setup = CreateHub("2");

        await setup.Hub.OnDisconnectedAsync(new InvalidOperationException("closed"));

        setup.Groups.Verify(groups => groups.RemoveFromGroupAsync(
            "connection-1",
            AuditLogHub.AdminGroupName,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static HubSetup CreateHub(string role)
    {
        var groups = new Mock<IGroupManager>();
        groups
            .Setup(manager => manager.AddToGroupAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groups
            .Setup(manager => manager.RemoveFromGroupAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(caller => caller.ConnectionId).Returns("connection-1");
        context.SetupGet(caller => caller.User).Returns(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role)],
                "UnitTest")));

        var hub = new AuditLogHub
        {
            Context = context.Object,
            Groups = groups.Object
        };

        return new HubSetup(hub, groups);
    }

    private sealed record HubSetup(AuditLogHub Hub, Mock<IGroupManager> Groups);
}
