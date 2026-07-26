using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SmartHealthMonitoring.Hubs;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.UnitTests;

public class ChatHubAuditTests
{
    [Fact]
    public async Task ClaimSession_AsDoctor_WritesActorAudit()
    {
        var setup = CreateHub();
        var session = Session();
        setup.Chat
            .Setup(service => service.ClaimSessionAsync(session.Id, 20))
            .ReturnsAsync(session);

        await setup.Hub.ClaimSession(session.Id);

        setup.Audit.Verify(service => service.LogForActorAsync(
            20,
            "Doctor Minh",
            "doctor@example.com",
            "Claim",
            "TelemedicineChatSession",
            session.Id.ToString(),
            It.Is<string>(description => description.Contains(session.Id.ToString())),
            session.PatientUserId,
            session.PatientUser.FullName,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task SendMessage_AsDoctor_WritesAuditForSavedMessage()
    {
        var setup = CreateHub();
        var session = Session();
        var sender = EntityFactory.User(20, 1, "Doctor Minh", "doctor@example.com");
        var saved = new TelemedicineChatMessage
        {
            Id = 9,
            SessionId = session.Id,
            SenderId = sender.Id,
            Sender = sender,
            ReceiverId = session.PatientUserId,
            Receiver = session.PatientUser,
            MessageContent = "Hello",
            SentAt = DateTime.UtcNow
        };
        setup.Chat.Setup(service => service.GetSessionAsync(session.Id)).ReturnsAsync(session);
        setup.Chat
            .Setup(service => service.SaveMessageAsync(session.Id, 20, "Hello"))
            .ReturnsAsync(saved);

        await setup.Hub.SendMessage(session.Id, " Hello ");

        setup.Audit.Verify(service => service.LogForActorAsync(
            20,
            "Doctor Minh",
            "doctor@example.com",
            "SendMessage",
            "TelemedicineChatMessage",
            saved.Id.ToString(),
            It.IsAny<string>(),
            session.PatientUserId,
            session.PatientUser.FullName,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task CloseSession_AsDoctor_WritesAudit()
    {
        var setup = CreateHub();
        var session = Session();
        setup.Chat.Setup(service => service.GetSessionAsync(session.Id)).ReturnsAsync(session);
        setup.Chat
            .Setup(service => service.CloseSessionAsync(session.Id, 20))
            .ReturnsAsync(true);

        await setup.Hub.CloseSession(session.Id);

        setup.Audit.Verify(service => service.LogForActorAsync(
            20,
            "Doctor Minh",
            "doctor@example.com",
            "Close",
            "TelemedicineChatSession",
            session.Id.ToString(),
            It.IsAny<string>(),
            session.PatientUserId,
            session.PatientUser.FullName,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task ConnectionLifecycle_ForDoctor_ManagesDoctorsGroup()
    {
        var setup = CreateHub();

        await setup.Hub.OnConnectedAsync();
        await setup.Hub.OnDisconnectedAsync(null);

        setup.Groups.Verify(manager => manager.AddToGroupAsync(
            "chat-connection",
            "Doctors",
            It.IsAny<CancellationToken>()), Times.Once);
        setup.Groups.Verify(manager => manager.RemoveFromGroupAsync(
            "chat-connection",
            "Doctors",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ChatHubSetup CreateHub()
    {
        var chat = new Mock<IChatService>();
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(service => service.LogForActorAsync(
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var proxy = new Mock<ISingleClientProxy>();
        proxy
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(value => value.Caller).Returns(proxy.Object);
        clients.Setup(value => value.User(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(proxy.Object);

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
        context.SetupGet(value => value.ConnectionId).Returns("chat-connection");
        context.SetupGet(value => value.UserIdentifier).Returns("20");
        context.SetupGet(value => value.Features).Returns(new FeatureCollection());
        context.SetupGet(value => value.User).Returns(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "1"),
                new Claim("FullName", "Doctor Minh"),
                new Claim(ClaimTypes.Email, "doctor@example.com")
            ], "UnitTest")));

        var hub = new ChatHub(chat.Object, audit.Object)
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object
        };

        return new ChatHubSetup(hub, chat, audit, groups);
    }

    private static TelemedicineChatSession Session()
    {
        var patient = EntityFactory.User(10, 0, "Patient Lan");
        var doctor = EntityFactory.User(20, 1, "Doctor Minh", "doctor@example.com");
        return new TelemedicineChatSession
        {
            Id = 5,
            PatientUserId = patient.Id,
            PatientUser = patient,
            DoctorUserId = doctor.Id,
            DoctorUser = doctor,
            Status = 1,
            ClaimedAt = DateTime.UtcNow
        };
    }

    private sealed record ChatHubSetup(
        ChatHub Hub,
        Mock<IChatService> Chat,
        Mock<IAuditLogService> Audit,
        Mock<IGroupManager> Groups);
}
