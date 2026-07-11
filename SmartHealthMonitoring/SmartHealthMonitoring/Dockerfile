# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file csproj và restore các gói thư viện
COPY ["SmartHealthMonitoring.csproj", "./"]
RUN dotnet restore "./SmartHealthMonitoring.csproj"

# Copy toàn bộ mã nguồn và build ứng dụng
COPY . .
RUN dotnet publish "SmartHealthMonitoring.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV TZ=Asia/Ho_Chi_Minh
ENTRYPOINT ["dotnet", "SmartHealthMonitoring.dll"]
