namespace Notification.Contracts;

public class NotificationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "email";
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}