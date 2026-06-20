using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Airsense.API.Models.Dto;
using Airsense.API.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airsense.API.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationController(INotificationRepository notificationRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery][Range(0, int.MaxValue, ErrorMessage = "The skip parameter must be a non-negative integer.")] int skip = 0,
        [FromQuery][Range(1, 100, ErrorMessage = "The count parameter must be between 1 and 100.")] int count = 20)
    {
        if (!int.TryParse(User.FindFirstValue("id"), out var userId))
            return BadRequest(new { message = "You are not registered" });

        var notifications = await notificationRepository.GetAsync(userId, count, skip);
        var totalCount = await notificationRepository.CountAsync(userId);

        return Ok(new PaginatedListDto
        {
            Data = notifications,
            Pagination = new PaginatedListDto.Metadata
            {
                Skip = skip,
                Count = notifications.Count,
                Total = totalCount
            }
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!int.TryParse(User.FindFirstValue("id"), out var userId))
            return BadRequest(new { message = "You are not registered" });

        return Ok(new { unread_count = await notificationRepository.CountUnreadAsync(userId) });
    }

    [HttpPatch("{notificationId:long}/read")]
    public async Task<IActionResult> MarkRead(long notificationId)
    {
        if (!int.TryParse(User.FindFirstValue("id"), out var userId))
            return BadRequest(new { message = "You are not registered" });

        var updated = await notificationRepository.MarkReadAsync(userId, notificationId);
        return updated ? NoContent() : NotFound(new { message = "Notification not found" });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (!int.TryParse(User.FindFirstValue("id"), out var userId))
            return BadRequest(new { message = "You are not registered" });

        var updated = await notificationRepository.MarkAllReadAsync(userId);
        return Ok(new { updated });
    }
}
