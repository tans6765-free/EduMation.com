using System.Security.Claims;
using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/messages")]
public sealed class AdminMessagesController(ApplicationDbContext db, IEmailSender email, ILogger<AdminMessagesController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? learningClassId, string? studentId)
    {
        var model = new AdminMessageViewModel { LearningClassId = learningClassId, StudentId = studentId };
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost("send"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(AdminMessageViewModel model, CancellationToken cancellationToken)
    {
        if ((model.StudentId == null) == (model.LearningClassId == null))
            ModelState.AddModelError(string.Empty, "Choose either one student or an entire class.");
        if (model.LearningClassId.HasValue && !await db.LearningClasses.AnyAsync(item => item.Id == model.LearningClassId, cancellationToken))
            ModelState.AddModelError(nameof(model.LearningClassId), "Choose a valid class.");

        var studentRoleIds = db.UserRoles.Join(db.Roles, link => link.RoleId, role => role.Id, (link, role) => new { link.UserId, role.Name })
            .Where(item => item.Name == "Student").Select(item => item.UserId);
        var recipients = from user in db.Users
            join profile in db.Profiles on user.Id equals profile.UserId into profileRows
            from profile in profileRows.DefaultIfEmpty()
            select new { User = user, Profile = profile };
        if (model.LearningClassId.HasValue) recipients = recipients.Where(row => studentRoleIds.Contains(row.User.Id) && row.Profile != null && row.Profile.LearningClassId == model.LearningClassId);
        if (model.StudentId != null) recipients = recipients.Where(row => row.User.Id == model.StudentId);
        var recipientRows = await recipients.Select(row => new
        {
            row.User.Id, row.User.Email, row.User.EmailConfirmed,
            FirstName = row.Profile == null ? row.User.UserName : row.Profile.FirstName,
            LastName = row.Profile == null ? string.Empty : row.Profile.LastName
        }).Take(1001).ToListAsync(cancellationToken);
        if (recipientRows.Count == 0) ModelState.AddModelError(string.Empty, "No student matches this recipient selection.");
        if (recipientRows.Count > 1000) ModelState.AddModelError(string.Empty, "A class email is limited to 1,000 students per send.");
        if (!ModelState.IsValid) { await PopulateAsync(model); return View("Index", model); }

        var delivered = 0;
        var failed = 0;
        var notificationText = model.Message.Trim();
        foreach (var row in recipientRows)
        {
            db.StudentNotifications.Add(new StudentNotification
            {
                UserId = row.Id, Title = model.Subject.Trim(), Message = notificationText.Length > 500 ? notificationText[..500] : notificationText,
                Link = "/notifications", BrowserNotification = true
            });
            if (!row.EmailConfirmed || string.IsNullOrWhiteSpace(row.Email)) { failed++; continue; }
            try
            {
                var name = $"{row.FirstName} {row.LastName}".Trim();
                await email.SendAsync(row.Email, model.Subject.Trim(), $"Hello {name},\n\n{notificationText}\n\nEduMation", cancellationToken);
                delivered++;
            }
            catch (Exception exception)
            {
                failed++;
                logger.LogWarning(exception, "Admin email delivery failed for a student recipient.");
            }
        }
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, Action = "Send account message", EntityName = model.LearningClassId.HasValue ? "StudentClass" : "User",
            EntityId = model.StudentId ?? $"class:{model.LearningClassId}",
            Summary = $"Recipients: {recipientRows.Count}; email sent: {delivered}; email skipped/failed: {failed}; subject: {model.Subject.Trim()}"
        });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = $"In-app notice queued for {recipientRows.Count} account(s); {delivered} emails sent and {failed} skipped or failed. Unverified accounts do not receive email.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(AdminMessageViewModel model)
    {
        model.Classes = await db.LearningClasses.AsNoTracking().OrderBy(item => item.DisplayOrder).ToListAsync();
        var options = from user in db.Users.AsNoTracking()
            join profile in db.Profiles.AsNoTracking() on user.Id equals profile.UserId into profileRows
            from profile in profileRows.DefaultIfEmpty()
            select new StudentRecipientOption(user.Id,
                profile == null ? (user.UserName ?? user.Email ?? "Account") : $"{profile.FirstName} {profile.LastName} ({user.UserName})",
                user.Email, profile == null ? null : profile.LearningClassId);
        model.Students = await options.OrderBy(item => item.DisplayName).ToListAsync();
    }
}
