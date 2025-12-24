namespace Notification.Contracts;

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Retrying
}

public class NotificationResult
{
    public Guid NotificationId { get; set; }
    public NotificationStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}