using Microsoft.AspNetCore.Mvc;
using Notification.Contracts;

namespace Notification.Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(ILogger<NotificationsController> logger)
    {
        _logger = logger;
    }

    // GET: api/notifications - для проверки работы
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Notification Gateway is running. Use POST to send notifications.");
    }

    // GET: api/notifications/{id} - для получения статуса (позже реализуем)
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return Ok($"Notification status for ID: {id} (not implemented yet)");
    }

    // POST: api/notifications - основной метод отправки
    [HttpPost]
    public IActionResult SendNotification([FromBody] NotificationRequest request)
    {
        try
        {
            // Валидация
            if (string.IsNullOrEmpty(request.To))
                return BadRequest("Recipient (To) is required");

            if (string.IsNullOrEmpty(request.Subject) && request.Type == "email")
                return BadRequest("Subject is required for email");

            if (string.IsNullOrEmpty(request.Body))
                return BadRequest("Message body is required");

            // Логируем
            _logger.LogInformation("Received notification: Type={Type}, To={To}, Subject={Subject}",
                request.Type, request.To, request.Subject);

            // TODO: Здесь позже добавим отправку в RabbitMQ
            // Пока просто возвращаем успех

            var response = new
            {
                Success = true,
                MessageId = request.Id,
                Message = "Notification accepted for processing",
                Type = request.Type,
                To = request.To,
                CreatedAt = request.CreatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification");
            return StatusCode(500, new { Error = "Internal server error", Details = ex.Message });
        }
    }
}