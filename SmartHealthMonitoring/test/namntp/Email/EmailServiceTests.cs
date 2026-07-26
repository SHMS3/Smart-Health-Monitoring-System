using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartHealthMonitoring.Models.Configurations;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.UnitTests;

public class EmailServiceTests
{
    [Fact]
    public void GetHtmlContentFromFile_WhenTemplateDoesNotExist_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        var result = service.GetHtmlContentFromFile(
            "missing.html",
            new Dictionary<string, string>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetHtmlContentFromFile_ReplacesEveryConfiguredToken()
    {
        using var temp = new TempDirectory();
        var root = System.IO.Path.Combine(temp.Path, "templates", "emails");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(root, "test.html"),
            "<h1>Hello {{Name}}</h1><p>{{Code}}/{{Name}}</p>");
        var service = CreateService(temp.Path);

        var result = service.GetHtmlContentFromFile(
            "test.html",
            new Dictionary<string, string>
            {
                ["{{Name}}"] = "Lan",
                ["{{Code}}"] = "A-01"
            });

        Assert.Equal("<h1>Hello Lan</h1><p>A-01/Lan</p>", result);
    }

    [Fact]
    public async Task SendEmailAsync_WhenAddressConfigurationIsInvalid_RethrowsError()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path, senderEmail: "not-an-email");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.SendEmailAsync("patient@example.com", "Subject", "<p>Body</p>"));
    }

    private static EmailService CreateService(
        string webRootPath,
        string senderEmail = "sender@example.com")
    {
        var settings = Options.Create(new EmailSettings
        {
            MailServer = "smtp.invalid",
            MailPort = 587,
            SenderName = "Smart Health",
            SenderEmail = senderEmail,
            Password = "not-used"
        });
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = webRootPath,
            WebRootPath = webRootPath
        };

        return new EmailService(
            settings,
            NullLogger<EmailService>.Instance,
            environment);
    }
}
