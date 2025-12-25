using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Contracts;
using Notification.EmailService.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Notification.SmsService;

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
        await InitializeRabbitMQAsync();

        _logger.LogInformation("SMS Service запущен. Ожидание сообщений...");

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
            queue: "sms_notifications",
            autoAck: false,
            consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task InitializeRabbitMQAsync()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: "sms_notifications",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("SMS Service подключен к RabbitMQ");
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
                _logger.LogWarning("Не удалось десериализовать SMS сообщение");
                _channel!.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            _logger.LogInformation("Начало обработки SMS уведомления {Id}", notification.Id);

            notificationLog = await dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);

            if (notificationLog == null)
            {
                notificationLog = NotificationLog.FromRequest(notification);
                dbContext.Notifications.Add(notificationLog);
                _logger.LogInformation("Создана новая запись для SMS {Id}", notification.Id);
            }
            else
            {
                notificationLog.MarkAsRetrying();
                _logger.LogInformation("Повторная обработка SMS {Id}, попытка {RetryCount}",
                    notification.Id, notificationLog.RetryCount);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await ProcessSmsNotificationAsync(notificationLog, cancellationToken);

            notificationLog.MarkAsSent();
            await dbContext.SaveChangesAsync(cancellationToken);

            _channel!.BasicAck(ea.DeliveryTag, false);

            _logger.LogInformation("SMS уведомление {Id} успешно обработано", notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке SMS уведомления");

            if (notificationLog != null)
            {
                notificationLog.MarkAsFailed(ex.Message);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Не удалось сохранить ошибку SMS в БД");
                }
            }

            _channel!.BasicNack(ea.DeliveryTag, false, true);
            _logger.LogInformation("SMS уведомление возвращено в очередь для повторной обработки");
        }
    }

    private async Task ProcessSmsNotificationAsync(NotificationLog notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(">>> Отправка SMS на номер: {To}", notification.To);
        _logger.LogInformation(">>> Текст SMS: {Body}",
            notification.Body?.Substring(0, Math.Min(100, notification.Body?.Length ?? 0)) + "...");

        await Task.Delay(300, cancellationToken);

        _logger.LogInformation(">>> SMS успешно отправлено");
    }

    public override void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();

            _connection?.Close();
            _connection?.Dispose();

            _logger.LogInformation("SMS Service: ресурсы RabbitMQ освобождены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при освобождении ресурсов SMS Service");
        }

        base.Dispose();
    }
}