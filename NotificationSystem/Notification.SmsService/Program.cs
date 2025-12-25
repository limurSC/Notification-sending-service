using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Notification.EmailService.Data;
using Notification.SmsService;

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
logger.LogInformation("=== SMS Service запускается ===");
logger.LogInformation("Окружение: {Environment}", builder.Environment.EnvironmentName);

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationContext>();

    try
    {
        logger.LogInformation("Проверка/применение миграций БД...");
        await dbContext.Database.MigrateAsync();

        var canConnect = await dbContext.Database.CanConnectAsync();
        logger.LogInformation("Подключение к БД: {Status}",
            canConnect ? "УСПЕШНО" : "НЕ УДАЛОСЬ");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при работе с БД");
        throw;
    }
}

host.Run();