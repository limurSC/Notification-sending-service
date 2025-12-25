using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Notification.Contracts;

namespace Notification.EmailService.Data;

[Table("Notifications")]
public class NotificationLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string To { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Subject { get; set; }

    [Column(TypeName = "text")]
    public string? Body { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public int RetryCount { get; set; }

    [Column(TypeName = "text")]
    public string? ErrorDetails { get; set; }

    [MaxLength(1000)]
    public string? AdditionalData { get; set; }

    public static NotificationLog FromRequest(NotificationRequest request)
    {
        return new NotificationLog
        {
            Id = request.Id,
            Type = request.Type,
            To = request.To,
            Subject = request.Subject,
            Body = request.Body,
            CreatedAt = request.CreatedAt,
            Status = "Pending",
            RetryCount = 0
        };
    }

    public void MarkAsSent()
    {
        Status = "Sent";
        SentAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error, bool incrementRetry = true)
    {
        Status = "Failed";
        ErrorDetails = error;
        if (incrementRetry)
            RetryCount++;
    }

    public void MarkAsRetrying()
    {
        Status = "Retrying";
        RetryCount++;
    }
}