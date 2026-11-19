using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

public class DiscordBotService(
    DiscordSocketClient client,
    InteractionService interaction,
    IConfiguration config,
    IServiceProvider services,
    ILogger<DiscordBotService> logger) : IHostedService
{
    private readonly DiscordSocketClient _client = client;
    private readonly InteractionService _interaction = interaction;
    private readonly IConfiguration _config = config;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<DiscordBotService> _logger = logger;

    private bool _commandsRegistered = false;

    // 봇 시작
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 이벤트 등록
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.InteractionCreated += InteractionCreatedAsync;

        // Discord Bot Token
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
                    ?? _config["DISCORD_TOKEN"];

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogCritical("Discord Token이 설정되지 않았습니다.");
            throw new InvalidOperationException("DISCORD_TOKEN이 설정되지 않았습니다.");
        }

        // Discord Guild ID
        var guildIdString = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID")
                            ?? _config["DISCORD_GUILD_ID"];

        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogCritical("Discord Guild ID가 설정되지 않았거나 올바르지 않습니다.");
            throw new InvalidOperationException("DISCORD_GUILD_ID가 올바르지 않습니다.");
        }

        // 명령어 모듈 등록
        var assembly = Assembly.GetEntryAssembly()
                       ?? Assembly.GetExecutingAssembly();

        await _interaction.AddModulesAsync(assembly, _services);

        _logger.LogInformation("슬래시 커맨드 모듈이 로드되었습니다.");

        // Discord 로그인 및 연결
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.LogInformation("Discord 연결을 시작했습니다.");
    }

    // 봇 종료
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("봇을 종료합니다...");

        await _client.SetStatusAsync(UserStatus.Offline);
        await _client.LogoutAsync();
        await _client.StopAsync();

        _logger.LogInformation("Discord 연결이 종료되었습니다.");
    }

    // Discord.Net 로그 처리
    private Task LogAsync(LogMessage log)
    {
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

    // Discord 연결 준비 완료
    private async Task ReadyAsync()
    {
        _logger.LogInformation(
            "{BotName} 봇이 준비되었습니다!",
            _client.CurrentUser.Username
        );

        // 활동 상태 설정
        await _client.SetActivityAsync(
            new Game("내가 돌아왔따~!", ActivityType.Playing)
        );

        await _client.SetStatusAsync(UserStatus.Online);

        // 명령어 중복 등록 방지
        if (_commandsRegistered)
            return;

        var guildIdString = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID")
                            ?? _config["DISCORD_GUILD_ID"];

        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogError("Discord Guild ID를 확인할 수 없습니다.");
            return;
        }

        // 지정된 서버에만 명령어 등록
        var guild = _client.GetGuild(guildId);

        if (guild is null)
        {
            _logger.LogError(
                "대상 Discord 서버를 찾을 수 없습니다. Guild ID: {GuildId}",
                guildId
            );

            return;
        }

        try
        {
            await _interaction.RegisterCommandsToGuildAsync(guildId);

            _commandsRegistered = true;

            _logger.LogInformation(
                "길드 명령어 등록 완료: {GuildName} ({GuildId})",
                guild.Name,
                guildId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "길드 명령어 등록에 실패했습니다.");
        }
    }

    // 일반 메시지 처리
    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
            return;

        if (message.Content == "!토리")
        {
            await message.Channel.SendMessageAsync(
                "여기, 여기! 토리 여기 있어!"
            );
        }
    }

    // 슬래시 명령어 처리
    private async Task InteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);

            var result = await _interaction.ExecuteCommandAsync(
                context,
                _services
            );

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "슬래시 커맨드 실행 실패: {ErrorReason}",
                    result.ErrorReason
                );

                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync(
                        "명령어 처리 중 오류가 발생했어요!",
                        ephemeral: true
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "인터랙션 처리 중 예외가 발생했습니다."
            );

            try
            {
                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync(
                        "명령어 처리 중 오류가 발생했어요!",
                        ephemeral: true
                    );
                }
            }
            catch (Exception responseException)
            {
                _logger.LogError(
                    responseException,
                    "인터랙션 오류 응답에 실패했습니다."
                );
            }
        }
    }
}