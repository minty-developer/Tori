using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

// 봇의 기동/로그인, 슬래시 커맨드 등록, 기본 텍스트 명령어("!토리")를 담당하는 핵심 호스팅 서비스.
public class DiscordBotService(
        DiscordSocketClient client,
        InteractionService interaction,
        IConfiguration config,
        IServiceProvider services,
        ILogger<DiscordBotService> logger) : IHostedService
{
    // 생성자에 주입만 해서 DI가 인스턴스를 계속 유지하게 한다 (번역 이벤트 구독 유지 목적)
    private readonly DiscordSocketClient _client = client;
    private readonly InteractionService _interaction = interaction;
    private readonly IConfiguration _config = config;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<DiscordBotService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.InteractionCreated += InteractionCreatedAsync;

        // 1. "활동 상태" 설정 (~ 플레이 중, ~ 시청 중 등)
        // ActivityType 종류: Playing(플레이 중), Streaming(방송 중), Listening(듣는 중), Watching(시청 중), Competing(경쟁 중)
        await _client.SetActivityAsync(new Game("내가 돌아왔따~!", ActivityType.Playing));

        // 2. "온라인 상태" 색상 표시 (Online, Idle, DoNotDisturb, Invisible 등)
        await _client.SetStatusAsync(UserStatus.Online);

    var token = _config["DISCORD_TOKEN"]
                    ?? Environment.GetEnvironmentVariable("DISCORD_TOKEN");
    if (string.IsNullOrEmpty(token))
        {
            _logger.LogCritical("DiscordToken이 설정되지 않았습니다! 환경 변수 또는 설정 파일을 확인해주세요.");
            throw new Exception("DiscordToken이 설정되지 않았습니다!");
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await _interaction.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        _logger.LogInformation("슬래시 커맨드 모듈이 로드되었습니다.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("봇을 종료합니다...");
        await _client.SetStatusAsync(UserStatus.Offline);
        await _client.LogoutAsync();
        await _client.StopAsync();
    }

    private Task LogAsync(LogMessage log)
    {
        // Discord.Net 내부 로그를 ILogger로도 남긴다 (Console.WriteLine만으로는 심각도 구분이 안 됨).
        var level = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(level, log.Exception, "[Discord.Net] {Message}", log.Message);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        _logger.LogInformation("{BotName} 봇이 준비되었습니다!", _client.CurrentUser.Username);

        foreach (var guild in _client.Guilds)
        {
            await guild.DeleteApplicationCommandsAsync();
        }

        await _interaction.RegisterCommandsGloballyAsync();

        _logger.LogInformation("길드 커맨드 청소 완료 및 전역 커맨드 등록이 완료되었습니다.");
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        // 기존 !토리 명령어 처리
        if (message.Content == "!토리")
        {
            await message.Channel.SendMessageAsync("여기, 여기! 토리 여기 있어!");
        }
    }

    private async Task InteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interaction.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
                _logger.LogWarning("슬래시 커맨드 실행 실패: {ErrorReason}", result.ErrorReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "인터랙션 처리 중 예외가 발생했습니다.");

            if (interaction.Type is InteractionType.ApplicationCommand)
            {
                await interaction.RespondAsync("명령어 처리 중 오류가 발생했어요!", ephemeral: true);
            }
        }
    }
}
