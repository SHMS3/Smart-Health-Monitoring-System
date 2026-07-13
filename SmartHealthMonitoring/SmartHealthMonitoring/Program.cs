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

            // 1. Đăng ký DB Context
            builder.Services.AddDbContext<SmartHealthMonitoringContext>(options =>
                options.UseSqlServer(connectionString ?? builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

            // 2. Cấu hình kết nối MinIO
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

            builder.Services.AddHttpClient<GeminiService>();

            // 3. MVC & Razor
            builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
            builder.Services.AddHttpContextAccessor();

            // 3b. SignalR cho Telemedicine Chat
            builder.Services.AddSignalR();

            // 4. Configure Email Settings
            builder.Services.Configure<SmartHealthMonitoring.Models.Configurations.EmailSettings>(
                builder.Configuration.GetSection("EmailSettings"));

            // 4b. HttpClient cho EsmsSmsService
            builder.Services.AddHttpClient();

            // 4c. Memory Cache & News Scraper Service
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<INewsScraperService, SmartHealthMonitoring.Services.NewsScraperService>();


            // ====================================================================
            // 5. GỌI HÀM QUÉT TỰ ĐỘNG TỪ THƯ MỤC DI
            // ====================================================================
            // ====================================================================
            builder.Services.AddApplicationServices();
            
            // Đăng ký Background Worker quét hồ sơ lâm sàng mới (DA-1.3)
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AI.AiPredictionWorker>();
            // Tắt Background Worker nhắc nhở ghi log chỉ số sinh hiệu để không tự động gửi email cho bệnh nhân.
            // builder.Services.AddHostedService<SmartHealthMonitoring.Workers.DailyVitalLogReminderWorker>();
            // Đặt lịch: sinh slot tự động mỗi ngày lúc 00:05
            // builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AppointmentSlotGeneratorWorker>();
            // Đặt lịch: dọn dẹp SoftLock hết hạn và đánh dấu No-show mỗi 2 phút
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AppointmentCleanupWorker>();
            // NTF-02: nhắc lịch hẹn Email/SMS trước 24h và 2h (quét mỗi 5 phút)
            builder.Services.AddHostedService<SmartHealthMonitoring.Workers.AppointmentReminderWorker>();

            // ====================================================================

            // 6.5. Session (dùng để lưu OTP xác thực số điện thoại)
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });


            // 6. Cookie Authentication
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


            // Chạy migration trong background để tránh block luồng Kestrel khởi chạy port binding (gây 502 Bad Gateway)
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
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Database] Auto-migration failed: {ex.Message}");
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

            // SignalR Hub endpoint cho Telemedicine Chat
            app.MapHub<ChatHub>("/chatHub");
            app.MapHub<AuditLogHub>("/auditLogHub");
            app.MapHub<AppointmentHub>("/appointmentHub");

            app.Run();
        }
    }
}
