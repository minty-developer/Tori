using Discord;
using Discord.Interactions;

public class HelpCommands(
    ILogger<HelpCommands> logger,
    InteractionService interactionService,
    Util util
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<HelpCommands> _logger = logger;
    private readonly InteractionService _interactionService = interactionService;
    private readonly Util _util = util;

    [SlashCommand("명령어", "토리가 할 수 있는 일은~!")]
    public async Task ListCommandsAsync()
    {
        var slashCommands = _interactionService.SlashCommands;

        var description = string.Join("\n", slashCommands.Select(cmd => $"`/{cmd.Name}` : {cmd.Description}"));

        var embed = new EmbedBuilder()
            .WithTitle("✨ 토리의 명령어 목록")
            .WithDescription(string.IsNullOrEmpty(description) ? "등록된 명령어가 없습니다." : description)
            .WithColor(Color.Parse("FF0033")) // 테토 시그니처 컬러
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("초대링크", "현재 채널의 서버 초대 링크를 생성합니다.")]
    public async Task CreateInviteLinkAsync(
        [Summary(description: "링크 만료 시간 (분 단위, 0 = 무제한)")] int maxAgeMinutes = 1440,
        [Summary(description: "최대 사용 횟수 (0 = 무제한)")] int maxUses = 0)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            if (Context.Channel is not ITextChannel channel)
            {
                await FollowupAsync("텍스트 채널에서만 초대 링크를 만들 수 있어!", ephemeral: true);
                return;
            }

            int? maxAgeSeconds = maxAgeMinutes > 0 ? maxAgeMinutes * 60 : null;
            int? uses = maxUses > 0 ? maxUses : null;

            var invite = await channel.CreateInviteAsync(
                maxAge: maxAgeSeconds,
                maxUses: uses,
                isTemporary: false,
                isUnique: true
            );

            await FollowupAsync($"짜잔! 초대 링크가 생성되었어!\n친구 많이 많이 데리고 오라구~!\n🔗 {invite.Url}", ephemeral: true);

            _logger.LogInformation("{User}님이 #{Channel} 채널의 초대 링크를 생성했습니다: {Url}", 
                Context.User.Username, channel.Name, invite.Url);
        }
        catch (Discord.Net.HttpException httpEx) when (httpEx.HttpCode == System.Net.HttpStatusCode.Forbidden)
        {
            await FollowupAsync("토리가 이 채널에서 **'초대 코드 만들기'** 권한이 없어!", ephemeral: true);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("초대링크", ex, "초대 링크를 생성하는 도중 오류가 발생했어!", _logger);
        }
    }
}