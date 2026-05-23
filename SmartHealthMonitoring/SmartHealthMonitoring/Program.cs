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

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
