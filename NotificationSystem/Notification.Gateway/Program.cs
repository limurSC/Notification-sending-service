using Microsoft.EntityFrameworkCore;
using Notification.EmailService.Data;
using Notification.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

builder.Services.AddDbContext<NotificationContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        Service = "Notification Gateway",
        Status = "Running",
        Version = "1.0",
        Endpoints = new[]
        {
            "/swagger - API Documentation",
            "/api/notifications - Send notifications",
            "/api/status/{id} - Check notification status",
            "/api/status/recent - Recent notifications",
            "/api/status/stats - Statistics"
        }
    });
});

app.Run();