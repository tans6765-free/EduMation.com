using Microsoft.AspNetCore.Mvc;

namespace EduMation.Controllers;

public class ProtijogController : Controller
{
    [HttpGet("/protijog-coming-soon")]
    public IActionResult ComingSoon()
    {
        return View();
    }
}
