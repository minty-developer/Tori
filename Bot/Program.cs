using Discord;
using Discord.WebSocket;
using Discord.Interactions;

// 앱 빌드
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

// 환경 변수 로드
BotEnv.CheckEnv();

// builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

// Discord.Net 소켓 클라이언트 설정 (게이트웨이 인텐트: 비특권 전체 + 메시지 본문 읽기 권한)
builder.Services.AddSingleton(x => new DiscordSocketConfig
{
    GatewayIntents =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMessages |
        GatewayIntents.MessageContent
});
builder.Services.AddSingleton(provider =>
    new DiscordSocketClient(provider.GetRequiredService<DiscordSocketConfig>())
);

// SQLite 커넥션/스키마 관리 서비스
builder.Services.AddSingleton<DatabaseService>();

// Util Halper 싱글톤
builder.Services.AddSingleton<Util>();

// Repository 싱글톤 등록
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<InformationRepository>();
builder.Services.AddSingleton<TitleRepository>();
builder.Services.AddSingleton<MoneyRepository>();
builder.Services.AddSingleton<VoteRepository>();

// 일본어 메시지 자동 번역 서비스. DiscordBotService 생성자에서 함께 주입받아
// DI 컨테이너가 이 인스턴스를 계속 살려두도록(=이벤트 구독이 유지되도록) 한다.
builder.Services.AddSingleton<TranslationService>();

// 매일 오전 9시 KST에 생일 알림을 전송하는 백그라운드 서비스
builder.Services.AddHostedService<BirthdayBackgroundService>();

// 디스코드 투표 임베드 관리 서비스
builder.Services.AddSingleton<PollMessageService>();

builder.Services.AddSingleton(provider =>
    new InteractionService(provider.GetRequiredService<DiscordSocketClient>())
);

// 메모리 캐시 사용 등록
builder.Services.AddMemoryCache();
// 컨트롤러 등록
builder.Services.AddControllers();

// 봇 로그인/슬래시 커맨드 등록/기본 이벤트 처리를 담당하는 메인 호스팅 서비스
builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

app.UseRateLimiterMiddleware();
app.UseAPIKeyMiddleware();

app.MapControllers();

// HTML 파일을 보낼 수 있게 설정
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/favicon.ico", () => Results.StatusCode(204));

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

app.Run(BotEnv.isDev? $"http://localhost:{port}": $"http://0.0.0.0:{port}");