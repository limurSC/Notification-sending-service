using System.Text;
using System.Text.Json;
using Notification.Contracts;
using RabbitMQ.Client;

namespace Notification.Gateway.Services;

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqService> _logger;

    public RabbitMqService(ILogger<RabbitMqService> logger)
    {
        _logger = logger;

        try
        {
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

            _channel.QueueDeclare(
                queue: "sms_notifications",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _logger.LogInformation("Подключение к RabbitMQ установлено");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подключении к RabbitMQ");
            throw;
        }
    }

    public void SendNotification(NotificationRequest notification)
    {
        try
        {
            var queueName = notification.Type.ToLower() switch
            {
                "email" => "email_notifications",
                "sms" => "sms_notifications",
                _ => "email_notifications"
            };

            var json = JsonSerializer.Serialize(notification);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            _logger.LogInformation("Уведомление {Id} отправлено в очередь {Queue}",
                notification.Id, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось отправить уведомление в RabbitMQ");
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();

            _connection?.Close();
            _connection?.Dispose();

            _logger.LogInformation("Подключение к RabbitMQ закрыто");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при закрытии подключения к RabbitMQ");
        }
    }
}