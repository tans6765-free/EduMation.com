using System.Security.Claims;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/welcome-email")]
public sealed class AdminWelcomeEmailController(ApplicationDbContext db) : Controller
{
    private const string TemplateSlug = "welcome-email-template";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var template = await FindTemplateAsync(cancellationToken);
        return template == null ? NotFound("Welcome email template has not been initialized. Restart EduMation once to seed it.") :
            View(new AdminWelcomeEmailViewModel { Subject = template.Title, Body = template.Body });
    }

    [HttpPost("save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminWelcomeEmailViewModel model, CancellationToken cancellationToken)
    {
        var template = await FindTemplateAsync(cancellationToken);
        if (template == null) return NotFound("Welcome email template has not been initialized.");
        if (!ModelState.IsValid) return View("Index", model);

        template.Title = model.Subject.Trim();
        template.Body = model.Body.Trim();
        template.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            Action = "Welcome email template updated", EntityName = "EmailTemplate", EntityId = TemplateSlug,
            Summary = "Updated the account welcome email template.", CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "Welcome email template saved.";
        return RedirectToAction(nameof(Index));
    }

    private Task<SitePage?> FindTemplateAsync(CancellationToken cancellationToken) =>
        db.SitePages.FirstOrDefaultAsync(page => page.Slug == TemplateSlug, cancellationToken);
}
