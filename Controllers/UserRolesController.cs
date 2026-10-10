using EduMation.ViewModel;
using EduMation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/users")]
public class UserRolesController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRolesController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.OrderBy(user => user.UserName).ToList();
        var model = new List<UserRoleViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Add(new UserRoleViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "Student"
            });
        }
        return View(model);
    }

    [HttpPost("{id}/role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string id, string role)
    {
        var allowedRoles = new[] { "Student", "Tutor", "Editor", "Admin" };
        if (!allowedRoles.Contains(role)) return BadRequest();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == id && role != "Admin")
        {
            TempData["Message"] = "You cannot remove your own administrator access.";
            return RedirectToAction(nameof(Index));
        }
        if (currentRoles.Contains("Admin") && role != "Admin" && (await _userManager.GetUsersInRoleAsync("Admin")).Count <= 1)
        {
            TempData["Message"] = "Keep at least one administrator account active.";
            return RedirectToAction(nameof(Index));
        }
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
        TempData["Message"] = "User role updated.";
        return RedirectToAction(nameof(Index));
    }
}
