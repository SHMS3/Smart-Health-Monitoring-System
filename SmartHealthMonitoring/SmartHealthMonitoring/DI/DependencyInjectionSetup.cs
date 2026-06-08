using Microsoft.Extensions.DependencyInjection;

namespace SmartHealthMonitoring.DI
{
    public static class DependencyInjectionSetup
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.Scan(scan => scan
                // 1. Quét toàn bộ project chứa class Program
                .FromAssemblyOf<Program>()

                // 2. Lọc ra các class có tên kết thúc bằng "Service" hoặc "Repository"
                .AddClasses(classes => classes.Where(type =>
                    type.Name.EndsWith("Service") || type.Name.EndsWith("Repository")))

                // 3. Đăng ký cho class có Interface (VD: IEmailService -> EmailService)
                .AsImplementedInterfaces()

                // 4. Đăng ký cho class KHÔNG có Interface (VD: UserRepository -> UserRepository)
                .AsSelf()

                // 5. Đặt vòng đời mặc định là Scoped (mỗi Request HTTP tạo ra 1 instance mới)
                .WithScopedLifetime());

            // TwilioVerifyService tự đăng ký qua Scrutor (ITwilioVerifyService)
            // Không cần đăng ký thêm ISmsService vì HomeController dùng ITwilioVerifyService

            // AI Services: ModelSessionRunner phải là Singleton để tái sử dụng ONNX Session
            services.AddSingleton<Services.AI.IAiModelSessionRunner, Services.AI.AiModelSessionRunner>();

            return services;
        }
    }
}
