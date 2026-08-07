using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bot.Api.Controllers;

[ApiController]
[Route("/api/v1")]
public class ApiController : ControllerBase
{
    [HttpGet]
    public IActionResult GetOpenApi()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "OpenApi.json");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "OpenApi.json 파일을 찾을 수 없습니다." });
        }

        // 파일 내용을 문자열로 읽어서 application/json 타입의 Content로 바로 반환
        var jsonString = System.IO.File.ReadAllText(filePath);
        return Content(jsonString, "application/json");
    }

    [HttpGet("/version")]
    public IActionResult GetCheckVersion() => Ok(new { version = BotEnv.botVersion });

    [HttpGet("/health")]
    public IActionResult GetCheckHealth() => Ok();
}