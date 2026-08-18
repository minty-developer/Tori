using Microsoft.AspNetCore.Mvc;

namespace Bot.Api.Controllers;

[ApiController]
[Route("/")]
public class UsersController : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/html");
    }
    [HttpGet("/docs")]
    public IActionResult Docs()
    {
        return RedirectPermanent("/api/v1");
    }
}