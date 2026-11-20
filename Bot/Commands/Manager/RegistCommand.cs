using Discord;
using Discord.Interactions;

[Group("명령어", "서버 명령어를 관리합니다.")]
public partial class RegistCommands(
    ILogger<RegistCommands> logger,
    InteractionService interactionService,
    Util util
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<RegistCommands> _logger = logger;
    private readonly Util _util = util;
    private readonly InteractionService _interactionService = interactionService;

    [SlashCommand("삭제", "슬래시 명령어를 정리합니다.")]
    public async Task DeleteCommandAsync(
        [Summary(description: "범위 선택")]
        [Choice("전역 정리", "global_clean")]
        [Choice("지역 정리", "local_clean")]
        [Choice("전체 정리", "all_clean")]
        string area
    )
    {
        try {
            if (Context.User is not IGuildUser user)
            {
                await RespondAsync("서버에서만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            if (!user.GuildPermissions.Administrator)
            {
                await RespondAsync("관리자만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            await DeferAsync();

            switch (area)
            {
                case "global_clean":
                    await _interactionService.RestClient.DeleteAllGlobalCommandsAsync();
                    break;
                case "local_clean":
                    if (Context.Guild is null)
                    {
                        await FollowupAsync(
                            "서버에서만 사용할 수 있는 기능이야!",
                            ephemeral: true);
                        return;
                    }
                    await _interactionService.RestClient
                        .BulkOverwriteGuildCommands([], Context.Guild.Id);
                    break;
                case "all_clean":
                    await _interactionService.RestClient
                        .DeleteAllGlobalCommandsAsync();

                    if (Context.Guild is not null)
                    {
                        await _interactionService.RestClient
                            .BulkOverwriteGuildCommands([], Context.Guild.Id);
                    }
                    break;
            }

            await FollowupAsync("🧹 정리를 완료했어!", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ 등록 중 오류가 발생했습니다: `{ex.Message}`", ephemeral: true);
        }
    }

    [SlashCommand("등록", "슬래시 명령어를 등록합니다.")]
    public async Task RegistCommandAsync(
        [Summary(description: "범위 선택")]
        [Choice("전역 등록", "global_regist")]
        [Choice("지역 등록", "local_regist")]
        [Choice("전체 등록", "all_regist")]
        string area
    )
    {
        try {
            if (Context.User is not IGuildUser user)
            {
                await RespondAsync("서버에서만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            if (!user.GuildPermissions.Administrator)
            {
                await RespondAsync("관리자만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            await DeferAsync();

            switch (area)
            {
                case "global_regist":
                    await _interactionService
                        .RegisterCommandsGloballyAsync();
                    break;
                case "local_regist":
                    if (Context.Guild is null)
                    {
                        await FollowupAsync(
                            "서버에서만 사용할 수 있는 기능이야!",
                            ephemeral: true);
                        return;
                    }
                    await _interactionService
                        .RegisterCommandsToGuildAsync(Context.Guild.Id);
                    break;
                case "all_regist":
                    await _interactionService.RestClient
                        .DeleteAllGlobalCommandsAsync();

                    if (Context.Guild is not null)
                    {
                        await _interactionService
                            .RegisterCommandsToGuildAsync(Context.Guild.Id);
                    }
                    break;
            }

            await FollowupAsync("➕ 등록을 완료했어!", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ 등록 중 오류가 발생했습니다: `{ex.Message}`", ephemeral: true);
        }
    }
}