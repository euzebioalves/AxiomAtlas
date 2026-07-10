using Axiom.Atlas.Application.DTOs.Notifications;
using Axiom.Atlas.Domain.Entities.Notifications;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Axiom.Atlas.API.Controllers.Notifications
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DesktopNotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DesktopNotificationsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var setting = await _context.UserDesktopNotificationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value);

            return Ok(new DesktopNotificationSettingsDto
            {
                IsEnabled = setting?.IsEnabled ?? false
            });
        }

        [HttpPut("settings")]
        public async Task<IActionResult> SaveSettings([FromBody] SaveDesktopNotificationSettingsRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var setting = await _context.UserDesktopNotificationSettings
                .FirstOrDefaultAsync(x => x.UserId == userId.Value);
            if (setting == null)
            {
                setting = new UserDesktopNotificationSetting { UserId = userId.Value };
                _context.UserDesktopNotificationSettings.Add(setting);
            }

            setting.IsEnabled = request.IsEnabled;
            setting.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new DesktopNotificationSettingsDto { IsEnabled = setting.IsEnabled });
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var enabled = await _context.UserDesktopNotificationSettings
                .AnyAsync(x => x.UserId == userId.Value && x.IsEnabled);
            if (!enabled)
            {
                return Ok(Array.Empty<DesktopNotificationDto>());
            }

            var notifications = await _context.DesktopNotifications
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value && x.DeliveredAt == null)
                .OrderBy(x => x.CreatedAt)
                .Take(20)
                .Select(x => new DesktopNotificationDto
                {
                    Id = x.Id,
                    WorkPackageId = x.WorkPackageId,
                    WorkPackageSubject = x.WorkPackageSubject,
                    StatusName = x.StatusName,
                    PreviousStatusName = x.PreviousStatusName,
                    WorkPackageUrl = x.WorkPackageUrl,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpPost("{id:guid}/delivered")]
        public async Task<IActionResult> MarkDelivered(Guid id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var notification = await _context.DesktopNotifications
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
            if (notification == null)
            {
                return NotFound();
            }

            if (!notification.DeliveredAt.HasValue)
            {
                notification.DeliveredAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        private Guid? GetCurrentUserId()
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;
        }
    }
}
