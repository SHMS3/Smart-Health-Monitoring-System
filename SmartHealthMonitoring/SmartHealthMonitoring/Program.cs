using DotNetEnv;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Minio;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.DI; // Gọi namespace DI của bạn
using System;

namespace SmartHealthMonitoring
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Env.Load();

            var builder = WebApplication.CreateBuilder(args);

            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION");

            // 1. Đăng ký DB Context
            builder.Services.AddDbContext<SmartHealthMonitoringContext>(options =>
                options.UseSqlServer(connectionString ?? builder.Configuration.GetConnectionString("DefaultConnection")));

            // 2. Cấu hình kết nối MinIO
            builder.Services.AddMinio(configureClient => configureClient
                .WithEndpoint("localhost:9000")
                .WithCredentials("admin", "admin123")
                .WithSSL(false)
                .Build());

            // 3. MVC & Razor
            builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

            // 4. Configure Email Settings
            builder.Services.Configure<SmartHealthMonitoring.Models.Configurations.EmailSettings>(
                builder.Configuration.GetSection("EmailSettings"));

            // ====================================================================
            // 5. GỌI HÀM QUÉT TỰ ĐỘNG TỪ THƯ MỤC DI
            // ====================================================================
            builder.Services.AddApplicationServices();
            // ====================================================================

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
            // Đăng ký Memory Cache cho Webhook
            builder.Services.AddMemoryCache();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<SmartHealthMonitoringContext>();
                 //SeedData.Initialize(context);
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                 name: "areas",
                 pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}