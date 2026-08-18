using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc;
using model;

namespace Bot.Api.Controllers;

[ApiController]
[Route("/api/v1")]
public class ApiController(DiscordSocketClient client) : ControllerBase
{
    private readonly DiscordSocketClient _client = client;

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

    [HttpGet("version")]
    public IActionResult GetCheckVersion() => Ok(new { version = BotEnv.botVersion });

    [HttpGet("health")]
    public IActionResult GetCheckHealth() => Ok();

    [HttpPost("announcement")]
    public async Task<IActionResult> SendAnnouncement(
        [FromBody] AnnouncementRequest request)
    {
        Console.WriteLine(
            $"공지 수신: {request.Version} / {request.Title}"
        );

        // 여기에 공지를 보낼 Discord 채널 ID
        ulong channelId = 1529006865260740729;

        var channel = await _client.GetChannelAsync(channelId);

        if (channel is not IMessageChannel messageChannel)
        {
            return NotFound(new
            {
                success = false,
                message = "Discord 채널을 찾을 수 없습니다."
            });
        }

        var embed = new EmbedBuilder()
            .WithTitle(request.Title)
            .WithDescription(request.Changes)
            .AddField("버전", request.Version, true)
            .AddField("업데이트 날짜", request.Date, true)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .Build();

        await messageChannel.SendMessageAsync(
            embed: embed
        );

        return Ok(new
        {
            success = true,
            message = "Discord 공지를 전송했습니다."
        });
    }
}