using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection.Metadata.Ecma335;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

BotEnv.CheckEnv();
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

// Discord.Net 소켓 클라이언트 설정 (게이트웨이 인텐트: 비특권 전체 + 메시지 본문 읽기 권한)
builder.Services.AddSingleton(x => new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
});

builder.Services.AddSingleton(provider =>
    new DiscordSocketClient(provider.GetRequiredService<DiscordSocketConfig>())
);

// SQLite 커넥션/스키마 관리 서비스 (Users, UserFishes 테이블)
builder.Services.AddSingleton<DatabaseService>();

// 매일 오전 9시 KST에 생일 알림을 전송하는 백그라운드 서비스
builder.Services.AddHostedService<BirthdayBackgroundService>();

builder.Services.AddSingleton(provider =>
    new InteractionService(provider.GetRequiredService<DiscordSocketClient>())
);

// 메모리 캐시 사용 등록
builder.Services.AddMemoryCache();
// 컨트롤러 등록
builder.Services.AddControllers();

// 일본어 메시지 자동 번역 서비스. DiscordBotService 생성자에서 함께 주입받아
// DI 컨테이너가 이 인스턴스를 계속 살려두도록(=이벤트 구독이 유지되도록) 한다.
builder.Services.AddSingleton<TranslationService>();

// 봇 로그인/슬래시 커맨드 등록/기본 이벤트 처리를 담당하는 메인 호스팅 서비스
builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

app.UseRateLimiterMiddleware();

// API Key 검증 미들웨어
app.Use(async (context, next) =>
{
    // /api 로 시작하는 요청에 대해서만 API 키 검증
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        // 1. Header에서 x-api-key 추출
        if (!context.Request.Headers.TryGetValue("x-api-key", out var extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "API 키가 누락되었습니다." });
            return;
        }

        var validApiKey = Environment.GetEnvironmentVariable("TORI_API_KEY");

        // 3. 키 일치 여부 검증
        if (string.IsNullOrEmpty(validApiKey) || !validApiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "유효하지 않은 API 키입니다." });
            return;
        }
    }

    // 검증 성공 시 다음 라우트/컨트롤러로 진행
    await next();
});

app.MapControllers();

// HTML 파일을 보낼 수 있게 설정
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/favicon.ico", () => Results.StatusCode(204));

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

app.Run(BotEnv.isDev? $"http://localhost:{port}": $"http://0.0.0.0:{port}");

public static class BotEnv
{
    public static bool isDev = true;
    public static string botVersion = "1.2.0";
    
    public static void CheckEnv() {
        if(isDev) Env.Load();
    }
}