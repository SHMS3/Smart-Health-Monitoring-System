using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.UnitTests;

internal static class TestContextFactory
{
    public static SmartHealthMonitoringContext Create()
    {
        var options = new DbContextOptionsBuilder<SmartHealthMonitoringContext>()
            .UseInMemoryDatabase($"SmartHealthMonitoring.UnitTests-{Guid.NewGuid():N}")
            .Options;

        return new SmartHealthMonitoringContext(options);
    }
}

internal static class ControllerTestExtensions
{
    public static T WithUser<T>(
        this T controller,
        int? userId,
        string? fullName = null,
        IServiceProvider? services = null,
        params string[] roles)
        where T : Controller
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Name, $"user{userId}@example.com"));
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            claims.Add(new Claim("FullName", fullName));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, authenticationType: "UnitTest");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services ?? new ServiceCollection().BuildServiceProvider()
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.Url = Mock.Of<IUrlHelper>();
        controller.TempData = new TempDataDictionary(httpContext, new DictionaryTempDataProvider());
        return controller;
    }
}

internal sealed class DictionaryTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object> _values = new();

    public IDictionary<string, object> LoadTempData(HttpContext context)
    {
        return new Dictionary<string, object>(_values);
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        _values.Clear();
        foreach (var item in values)
        {
            _values[item.Key] = item.Value;
        }
    }
}

internal static class EntityFactory
{
    public static User User(
        int id,
        byte role,
        string? fullName = null,
        string? email = null,
        string passwordHash = "password")
    {
        return new User
        {
            Id = id,
            FullName = fullName ?? $"User {id}",
            Email = email ?? $"user{id}@example.com",
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)
        };
    }

    public static Patient Patient(int id, User user)
    {
        return new Patient
        {
            Id = id,
            UserId = user.Id,
            User = user,
            DateOfBirth = new DateOnly(1990, 1, 1)
        };
    }

    public static Doctor Doctor(int id, User user)
    {
        return new Doctor
        {
            Id = id,
            UserId = user.Id,
            User = user,
            Specialty = "Cardiology",
            RoomNumber = "P.201"
        };
    }
}
