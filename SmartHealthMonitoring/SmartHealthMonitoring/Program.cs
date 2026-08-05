using DotNetEnv;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Minio;
using SmartHealthMonitoring.DI; // Gọi namespace DI của bạn
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.Hubs;
using System;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Filters;

namespace SmartHealthMonitoring
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var envState = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            if (envState.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                Env.Load();
            }

            var builder = WebApplication.CreateBuilder(args);

            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION");

            builder.Services.AddDbContext<SmartHealthMonitoringContext>(options =>
                options.UseSqlServer(connectionString ?? builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

            var minioEndpoint = builder.Configuration["MinioSettings:Endpoint"] ?? Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "localhost:9000";
            var minioAccessKey = builder.Configuration["MinioSettings:AccessKey"] ?? Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? "admin";
            var minioSecretKey = builder.Configuration["MinioSettings:SecretKey"] ?? Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? "admin123";
            var minioSecureStr = builder.Configuration["MinioSettings:Secure"] ?? Environment.GetEnvironmentVariable("MINIO_SECURE") ?? "false";
            bool minioSecure = minioSecureStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            builder.Services.AddMinio(configureClient => configureClient
                .WithEndpoint(minioEndpoint)
                .WithCredentials(minioAccessKey, minioSecretKey)
                .WithSSL(minioSecure)
                .Build());

            builder.Services.AddHttpClient<SmartHealthMonitoring.Services.AI.GeminiService>();
            builder.Services.AddScoped<SmartHealthMonitoring.Services.QR.LocalOcrService>();

            builder.Services.AddScoped<AuditLogActionFilter>();
            builder.Services
                .AddControllersWithViews(options =>
                    options.Filters.AddService<AuditLogActionFilter>())
                .AddRazorRuntimeCompilation();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddSignalR();

            builder.Services.Configure<SmartHealthMonitoring.Models.Configurations.EmailSettings>(
                builder.Configuration.GetSection("EmailSettings"));

            builder.Services.AddHttpClient();

            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<SmartHealthMonitoring.Interfaces.News.INewsScraperService, SmartHealthMonitoring.Services.News.NewsScraperService>();


            builder.Services.AddApplicationServices();
            
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AI.AiPredictionWorker>();
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AppointmentSlotGeneratorWorker>();
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AppointmentCleanupWorker>();
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.DailyVitalLogReminderWorker>();

            if (!builder.Environment.IsDevelopment() ||
                builder.Configuration.GetValue<bool>("BackgroundWorkers:EnableNotifications"))
            {
                builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AppointmentReminderWorker>();
            }


            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });


            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Auth/Login";
                    options.LogoutPath = "/Auth/Logout";
                    options.AccessDeniedPath = "/Auth/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(24);
                    options.SlidingExpiration = true;
                }).AddGoogle(options =>
                {
                    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                    options.CallbackPath = "/Auth/GoogleCallback";
                });

            var app = builder.Build();


            _ = Task.Run(() =>
            {
                using (var scope = app.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    try
                    {
                        var context = services.GetRequiredService<SmartHealthMonitoringContext>();
                        context.Database.Migrate();
                        Console.WriteLine("[Database] Auto-migration successfully executed.");

                        if (!context.DoctorWorkSchedules.Any())
                        {
                            var schedules = new List<SmartHealthMonitoring.Models.DoctorWorkSchedule>();
                            for (int docId = 1; docId <= 10; docId++)
                            {
                                for (byte dayOfWeek = 1; dayOfWeek <= 5; dayOfWeek++)
                                {
                                    schedules.Add(new SmartHealthMonitoring.Models.DoctorWorkSchedule
                                    {
                                        DoctorId = docId,
                                        DayOfWeek = dayOfWeek,
                                        StartTime = new TimeOnly(8, 0),
                                        EndTime = new TimeOnly(12, 0),
                                        SlotDurationMinutes = 30,
                                        IsActive = true
                                    });
                                    schedules.Add(new SmartHealthMonitoring.Models.DoctorWorkSchedule
                                    {
                                        DoctorId = docId,
                                        DayOfWeek = dayOfWeek,
                                        StartTime = new TimeOnly(13, 30),
                                        EndTime = new TimeOnly(17, 0),
                                        SlotDurationMinutes = 30,
                                        IsActive = true
                                    });
                                }
                            }
                            context.DoctorWorkSchedules.AddRange(schedules);
                            context.SaveChanges();
                            Console.WriteLine("[Database] Auto-seeded DoctorWorkSchedules.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Database] Error during auto-migration/seeding: {ex.Message}");
                    }
                }
            });

            
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseSession(); // Session phải trước UseRouting để hoạt động đúng

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                 name: "areas",
                 pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapHub<ChatHub>("/chatHub");
            app.MapHub<AuditLogHub>("/auditLogHub");
            app.MapHub<AppointmentHub>("/appointmentHub");

            app.Run();
        }
    }
}
