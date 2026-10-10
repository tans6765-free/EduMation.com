using System.Security.Claims;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/pages")]
public sealed class AdminPagesController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await db.SitePages.AsNoTracking().Where(item => item.Slug != "welcome-email-template").OrderBy(item => item.Slug).ToListAsync(cancellationToken));

    [HttpGet("create")]
    public IActionResult Create() => View("Edit", new SitePageViewModel());

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SitePageViewModel model, CancellationToken cancellationToken)
    {
        if (await db.SitePages.AnyAsync(item => item.Slug == model.Slug, cancellationToken)) ModelState.AddModelError(nameof(model.Slug), "That page address is already in use.");
        if (!ModelState.IsValid) return View("Edit", model);
        var page = new SitePage { Slug = model.Slug, Title = model.Title.Trim(), MetaDescription = model.MetaDescription.Trim(), Body = model.Body.Trim(), IsPublished = model.IsPublished };
        db.SitePages.Add(page);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("Page created", page, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var page = await db.SitePages.FindAsync([id], cancellationToken);
        return page == null || page.Slug == "welcome-email-template" ? NotFound() : View(new SitePageViewModel { Id = page.Id, Slug = page.Slug, Title = page.Title, MetaDescription = page.MetaDescription, Body = page.Body, IsPublished = page.IsPublished });
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SitePageViewModel model, CancellationToken cancellationToken)
    {
        var page = await db.SitePages.FindAsync([id], cancellationToken);
        if (page == null || page.Slug == "welcome-email-template") return NotFound();
        if (await db.SitePages.AnyAsync(item => item.Id != id && item.Slug == model.Slug, cancellationToken)) ModelState.AddModelError(nameof(model.Slug), "That page address is already in use.");
        if (!ModelState.IsValid) return View(model);
        page.Slug = model.Slug; page.Title = model.Title.Trim(); page.MetaDescription = model.MetaDescription.Trim(); page.Body = model.Body.Trim(); page.IsPublished = model.IsPublished; page.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("Page updated", page, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/unpublish"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(int id, CancellationToken cancellationToken)
    {
        var page = await db.SitePages.FindAsync([id], cancellationToken);
        if (page == null) return NotFound();
        page.IsPublished = false; page.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("Page unpublished", page, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task AuditAsync(string action, SitePage page, CancellationToken cancellationToken)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog { AdminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, Action = action, EntityName = "SitePage", EntityId = page.Id.ToString(), Summary = $"{action}: {page.Slug}", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
    }
}
