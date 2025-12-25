using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.EmailService.Data;
using Notification.Gateway.Services;

namespace Notification.Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly NotificationContext _dbContext;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        NotificationContext dbContext,
        ILogger<StatusController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNotificationStatus(Guid id)
    {
        try
        {
            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = $"Уведомление с ID {id} не найдено"
                });
            }

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    notification.Id,
                    notification.Type,
                    notification.To,
                    notification.Subject,
                    BodyPreview = notification.Body?.Length > 100
                        ? notification.Body.Substring(0, 100) + "..."
                        : notification.Body,
                    notification.Status,
                    notification.CreatedAt,
                    notification.SentAt,
                    notification.RetryCount,
                    notification.ErrorDetails,
                    IsSuccess = notification.Status == "Sent",
                    IsFailed = notification.Status == "Failed",
                    IsPending = notification.Status == "Pending",
                    Duration = notification.SentAt.HasValue
                        ? (notification.SentAt.Value - notification.CreatedAt).TotalSeconds
                        : (double?)null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса уведомления {Id}", id);
            return StatusCode(500, new
            {
                Success = false,
                Message = "Внутренняя ошибка сервера при получении статуса"
            });
        }
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentNotifications(
        [FromQuery] int limit = 10,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null)
    {
        try
        {
            var query = _dbContext.Notifications.AsQueryable();

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(n => n.Type.ToLower() == type.ToLower());
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(n => n.Status.ToLower() == status.ToLower());
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new
                {
                    n.Id,
                    n.Type,
                    n.To,
                    n.Subject,
                    BodyPreview = n.Body != null && n.Body.Length > 50
                        ? n.Body.Substring(0, 50) + "..."
                        : n.Body,
                    n.Status,
                    n.CreatedAt,
                    n.SentAt,
                    n.RetryCount,
                    HasError = !string.IsNullOrEmpty(n.ErrorDetails)
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Count = notifications.Count,
                TotalInDb = await _dbContext.Notifications.CountAsync(),
                Data = notifications
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка уведомлений");
            return StatusCode(500, new
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] int hours = 24)
    {
        try
        {
            var fromDate = DateTime.UtcNow.AddHours(-hours);

            var stats = await _dbContext.Notifications
                .Where(n => n.CreatedAt >= fromDate)
                .GroupBy(n => n.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    AvgProcessingTime = g
                        .Where(x => x.SentAt.HasValue)
                        .Average(x => (x.SentAt!.Value - x.CreatedAt).TotalSeconds)
                })
                .ToListAsync();

            var total = await _dbContext.Notifications
                .Where(n => n.CreatedAt >= fromDate)
                .CountAsync();

            var byType = await _dbContext.Notifications
                .Where(n => n.CreatedAt >= fromDate)
                .GroupBy(n => n.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    SuccessRate = g.Count(x => x.Status == "Sent") * 100.0 / g.Count()
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Period = $"{hours} часов",
                FromDate = fromDate,
                Total = total,
                ByStatus = stats,
                ByType = byType,
                RecentFailures = await _dbContext.Notifications
                    .Where(n => n.Status == "Failed" && n.CreatedAt >= fromDate)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .Select(n => new
                    {
                        n.Id,
                        n.Type,
                        n.To,
                        n.ErrorDetails,
                        n.CreatedAt
                    })
                    .ToListAsync()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики");
            return StatusCode(500, new
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            });
        }
    }
}