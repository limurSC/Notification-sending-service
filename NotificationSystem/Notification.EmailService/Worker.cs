using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Contracts;
using Notification.EmailService.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Notification.EmailService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _connection;
    private IModel? _channel;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Service запущен. Ожидание RabbitMQ...");

        await WaitForRabbitMQAsync(stoppingToken);

        await InitializeRabbitMQAsync();

        _logger.LogInformation("Email Service готов к работе. Ожидание сообщений...");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            try
            {
                await ProcessMessageAsync(ea, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Необработанная ошибка в обработчике сообщения");
                _channel?.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel!.BasicConsume(
            queue: "email_notifications",
            autoAck: false,
            consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task WaitForRabbitMQAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 30;
        const int delayMs = 2000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Попытка подключения к RabbitMQ ({Attempt}/{MaxRetries})...",
                    attempt, maxRetries);

                var factory = new ConnectionFactory
                {
                    HostName = "rabbitmq",
                    Port = 5672,
                    UserName = "guest",
                    Password = "guest",
                    DispatchConsumersAsync = true,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                _logger.LogInformation("RabbitMQ доступен!");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("RabbitMQ еще не готов. Ожидание {Delay}ms... Ошибка: {Message}",
                    delayMs, ex.Message);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось подключиться к RabbitMQ после {MaxRetries} попыток",
                    maxRetries);
                throw;
            }
        }
    }

    private async Task InitializeRabbitMQAsync()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = "rabbitmq",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: "email_notifications",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("Подключение к RabbitMQ установлено");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подключении к RabbitMQ");
            throw;
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationContext>();

        NotificationLog? notificationLog = null;

        try
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var notification = JsonSerializer.Deserialize<NotificationRequest>(message);

            if (notification == null)
            {
                _logger.LogWarning("Не удалось десериализовать сообщение. Сообщение: {Message}", message);
                _channel!.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            _logger.LogInformation("Начало обработки уведомления {Id}", notification.Id);

            notificationLog = await dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);

            if (notificationLog == null)
            {
                notificationLog = NotificationLog.FromRequest(notification);
                dbContext.Notifications.Add(notificationLog);
                _logger.LogInformation("Создана новая запись для уведомления {Id}", notification.Id);
            }
            else
            {
                notificationLog.MarkAsRetrying();
                _logger.LogInformation("Повторная обработка уведомления {Id}, попытка {RetryCount}",
                    notification.Id, notificationLog.RetryCount);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await ProcessEmailNotificationAsync(notificationLog, cancellationToken);

            notificationLog.MarkAsSent();
            await dbContext.SaveChangesAsync(cancellationToken);

            _channel!.BasicAck(ea.DeliveryTag, false);

            _logger.LogInformation("Уведомление {Id} успешно обработано и сохранено", notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке уведомления");

            if (notificationLog != null)
            {
                notificationLog.MarkAsFailed(ex.Message);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Не удалось сохранить ошибку в БД");
                }
            }

            _channel!.BasicNack(ea.DeliveryTag, false, true);

            _logger.LogInformation("Уведомление возвращено в очередь для повторной обработки");
        }
    }

    private async Task ProcessEmailNotificationAsync(NotificationLog notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(">>> Отправка email для {To}", notification.To);
        _logger.LogInformation(">>> Тема: {Subject}", notification.Subject);
        _logger.LogInformation(">>> Текст: {Body}", notification.Body?.Substring(0, Math.Min(50, notification.Body?.Length ?? 0)) + "...");

        await Task.Delay(500, cancellationToken);

        _logger.LogInformation(">>> Email успешно отправлен");
    }

    public override void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();

            _connection?.Close();
            _connection?.Dispose();

            _logger.LogInformation("Ресурсы RabbitMQ освобождены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при освобождении ресурсов");
        }

        base.Dispose();
    }
}