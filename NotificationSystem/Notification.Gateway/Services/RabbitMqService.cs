using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Notification.Contracts;
using RabbitMQ.Client;

namespace Notification.Gateway.Services;

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqService> _logger;
    private readonly RabbitMqSettings _settings;

    public RabbitMqService(
        ILogger<RabbitMqService> logger,
        IOptions<RabbitMqSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
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

            _channel.QueueDeclare(
                queue: "sms_notifications",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _logger.LogInformation("Gateway подключился к RabbitMQ: {Host}:{Port}",
                _settings.HostName, _settings.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway: ошибка при подключении к RabbitMQ {Host}:{Port}",
                _settings.HostName, _settings.Port);
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

            _logger.LogInformation("Gateway: уведомление {Id} отправлено в очередь {Queue}",
                notification.Id, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway: не удалось отправить уведомление в RabbitMQ");
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

            _logger.LogInformation("Gateway: подключение к RabbitMQ закрыто");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway: ошибка при закрытии подключения к RabbitMQ");
        }
    }
}

public class RabbitMqSettings
{
    public string HostName { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}