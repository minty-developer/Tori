using Discord;
using Discord.WebSocket;
using Discord.Interactions;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

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

// 일본어 메시지 자동 번역 서비스. DiscordBotService 생성자에서 함께 주입받아
// DI 컨테이너가 이 인스턴스를 계속 살려두도록(=이벤트 구독이 유지되도록) 한다.
builder.Services.AddSingleton<TranslationService>();

// 봇 로그인/슬래시 커맨드 등록/기본 이벤트 처리를 담당하는 메인 호스팅 서비스
builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

app.MapGet("/", () => "토리 여기 있다구!");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

Console.WriteLine("토리가 돌아왔따~!!");
app.Run($"http://0.0.0.0:{port}");
