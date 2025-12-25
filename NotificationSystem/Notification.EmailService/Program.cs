using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Notification.EmailService;
using Notification.EmailService.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "[HH:mm:ss] ";
    options.SingleLine = true;
});

builder.Services.AddDbContext<NotificationContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== Email Service запускается ===");
logger.LogInformation("Окружение: {Environment}", builder.Environment.EnvironmentName);
logger.LogInformation("Корневой каталог: {ContentRoot}", builder.Environment.ContentRootPath);

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationContext>();

    try
    {
        logger.LogInformation("Применяем миграции базы данных...");
        dbContext.Database.Migrate();
        logger.LogInformation("Миграции успешно применены");

        var canConnect = await dbContext.Database.CanConnectAsync();
        logger.LogInformation("Подключение к БД: {Status}",
            canConnect ? "УСПЕШНО" : "НЕ УДАЛОСЬ");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при применении миграций");
        throw;
    }
}

host.Run();