using System.Security.Claims;
using EduMation.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Authorize]
[Route("notifications")]
public sealed class NotificationsController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var items = await db.StudentNotifications.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc).Take(100).ToListAsync();
        return View(items);
    }

    [HttpGet("unread")]
    public async Task<IActionResult> Unread()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var items = await db.StudentNotifications.AsNoTracking().Where(item => item.UserId == userId && item.ReadAtUtc == null && item.BrowserNotification)
            .OrderBy(item => item.CreatedAtUtc).Take(20).Select(item => new { item.Id, item.Title, item.Message, item.Link, item.CreatedAtUtc }).ToListAsync();
        return Json(items);
    }

    [HttpPost("{id:int}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var notification = await db.StudentNotifications.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (notification == null) return NotFound();
        notification.ReadAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
