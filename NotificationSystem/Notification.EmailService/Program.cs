using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Notification.EmailService.Data;
using Notification.EmailService;

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


host.Run();