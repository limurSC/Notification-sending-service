using Notification.Contracts;

namespace Notification.Gateway.Services;

public interface IRabbitMqService
{
    void SendNotification(NotificationRequest notification);
}