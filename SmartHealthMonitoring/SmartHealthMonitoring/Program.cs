using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Minio;
using SmartHealthMonitoring.Context;
//using SmartHealthMonitoring.Services;
using System;

namespace SmartHealthMonitoring
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Cấu hình kết nối MinIO
            builder.Services.AddMinio(configureClient => configureClient
                .WithEndpoint("localhost:9000") 
                .WithCredentials("admin", "admin123") 
                .WithSSL(false) // Đang chạy localhost nên tắt SSL
                .Build());

            // 2. Đăng ký MinioService
            //builder.Services.AddScoped<IMinioService, MinioService>();

            // Add services to the container.
            //builder.Services.AddControllersWithViews();
            builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

            // Configure Email Settings and Service
            builder.Services.Configure<SmartHealthMonitoring.Models.Configurations.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddScoped<SmartHealthMonitoring.Services.IEmailService, SmartHealthMonitoring.Services.EmailService>();

            // Cookie Authentication + Google OAuth
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Auth/Login";
                    options.LogoutPath = "/Auth/Logout";
                    options.AccessDeniedPath = "/Auth/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(24);
                    options.SlidingExpiration = true;
                })
                .AddGoogle(options =>
                {
                    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                    options.CallbackPath = "/Auth/GoogleCallback";
                });

            //Đki db
            builder.Services.AddDbContext<SmartHealthMonitoringContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<SmartHealthMonitoringContext>();

               // SeedData.Initialize(context);
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
