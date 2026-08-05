using Microsoft.AspNetCore.Mvc;

namespace MyBot.Api.Controllers;

[ApiController]
[Route("/")]
public class UsersController : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/html");
    }
}