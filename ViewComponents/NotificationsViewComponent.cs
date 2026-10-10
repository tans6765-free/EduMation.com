using System.Security.Claims;
using EduMation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.ViewComponents;

public sealed class NotificationsViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (UserClaimsPrincipal.Identity?.IsAuthenticated != true) return Content(string.Empty);
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = await db.StudentNotifications.CountAsync(item => item.UserId == userId && item.ReadAtUtc == null);
        return View(unread);
    }
}
