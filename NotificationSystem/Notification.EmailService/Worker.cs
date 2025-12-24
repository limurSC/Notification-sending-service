using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Notification.EmailService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: "email_notifications",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("Email Service подключен к RabbitMQ");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Service запущен. Ожидание сообщений...");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var retryCount = 0;
            const int maxRetries = 3;
            bool processed = false;

            while (!processed && retryCount < maxRetries)
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var notification = JsonSerializer.Deserialize<NotificationRequest>(message);

                    if (notification != null)
                    {
                        _logger.LogInformation("Попытка {Attempt} обработки уведомления {Id}",
                            retryCount + 1, notification.Id);

                        await ProcessEmailNotificationAsync(notification);

                        _channel.BasicAck(ea.DeliveryTag, false);
                        processed = true;

                        _logger.LogInformation("Уведомление {Id} обработано успешно", notification.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Не удалось десериализовать сообщение");
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                        processed = true;
                    }
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Ошибка при обработке (попытка {Attempt}). Повтор через 2 секунды",
                        retryCount);
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Все {MaxRetries} попытки обработки уведомления провалились",
                        maxRetries);
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                    processed = true;
                }
            }
        };

        _channel.BasicConsume(
            queue: "email_notifications",
            autoAck: false,
            consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessEmailNotificationAsync(NotificationRequest notification)
    {
        await Task.Delay(100);

        _logger.LogInformation("Обработка email для {To}: {Subject}",
            notification.To, notification.Subject);
    }

    private void ProcessEmailNotification(NotificationRequest notification)
    {
        try
        {
            _logger.LogInformation("=== ОБРАБОТКА EMAIL ===");
            _logger.LogInformation("ID: {Id}", notification.Id);
            _logger.LogInformation("Кому: {To}", notification.To);
            _logger.LogInformation("Тема: {Subject}", notification.Subject);
            _logger.LogInformation("Текст: {Body}", notification.Body);
            _logger.LogInformation("Тип: {Type}", notification.Type);
            _logger.LogInformation("Создано: {CreatedAt}", notification.CreatedAt);
            _logger.LogInformation("=== КОНЕЦ ОБРАБОТКИ ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в ProcessEmailNotification для уведомления {Id}", notification.Id);
            throw;
        }
    }

    public override void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();

            _connection?.Close();
            _connection?.Dispose();

            _logger.LogInformation("Email Service отключен от RabbitMQ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отключении от RabbitMQ");
        }

        base.Dispose();
    }
}