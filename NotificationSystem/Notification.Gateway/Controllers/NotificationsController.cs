using Microsoft.AspNetCore.Mvc;
using Notification.Contracts;
using Notification.Gateway.Services;

namespace Notification.Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ILogger<NotificationsController> _logger;
    private readonly IRabbitMqService _mqService;
    
    public NotificationsController(
        ILogger<NotificationsController> logger,
        IRabbitMqService mqService)
    {
        _logger = logger;
        _mqService = mqService;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Notification Gateway is running");
    }

    [HttpPost]
    public IActionResult SendNotification([FromBody] NotificationRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.To))
                return BadRequest("Получатель (To) обязателен");

            if (string.IsNullOrEmpty(request.Subject) && request.Type == "email")
                return BadRequest("Тема обязательна для email");

            _mqService.SendNotification(request);

            _logger.LogInformation("Получено уведомление: {Type} для {To}",
                request.Type, request.To);

            return Ok(new
            {
                Success = true,
                MessageId = request.Id,
                Message = "Уведомление принято в обработку",
                Type = request.Type,
                Queue = request.Type.ToLower() == "email"
                    ? "email_notifications"
                    : "sms_notifications",
                CreatedAt = request.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке уведомления");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}