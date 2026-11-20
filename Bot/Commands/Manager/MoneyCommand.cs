using Discord;
using Discord.Interactions;

[Group("돈", "서버 재화를 관리합니다.")]
public partial class MoneyCommands(
    MoneyRepository moneyRepository,
    ILogger<MoneyCommands> logger,
    Util util
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly MoneyRepository _repo = moneyRepository;
    private readonly ILogger<MoneyCommands> _logger = logger;
    private readonly Util _util = util;

    [SlashCommand("지급", "유저의 재화를 지급합니다.")]
    public async Task AddMoneyAsync(
        [Summary(description: "대상 유저")] IUser targetUser,
        [Summary(description: "돈")] long amount
    )
    {
        try
        {
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

            if (amount < 0)
            {
                await RespondAsync("액수는 0보다 커야 해!", ephemeral: true);
                return;
            }
            await _repo.AddPointsAsync(targetUser.Id, (int)amount);
            await RespondAsync($"💰 **{targetUser.Username}**의 포인트를 지급했어!", ephemeral: true);
            _logger.LogInformation("{Admin}님이 {Target}님의 포인트를 지급했습니다.", Context.User.Username, targetUser.Username);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("돈관리", ex, "유저의 돈을 관리하는 과정에서 에러가 발생했습니다.", _logger);
        }
    }

    [SlashCommand("차감", "유저의 재화를 차감합니다.")]
    public async Task RemoveMoneyAsync(
        [Summary(description: "대상 유저")] IUser targetUser,
        [Summary(description: "돈")] long amount
    )
    {
        try
        {
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

            if (amount < 0)
            {
                await RespondAsync("액수는 0보다 커야 해!", ephemeral: true);
                return;
            }
            await _repo.RemovePointsAsync(targetUser.Id, (int)amount);
            await RespondAsync($"💸 **{targetUser.Username}**의 포인트를 차감했어!", ephemeral: true);
            _logger.LogInformation("{Admin}님이 {Target}님의 포인트를 지급했습니다.", Context.User.Username, targetUser.Username);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("돈관리", ex, "유저의 돈을 관리하는 과정에서 에러가 발생했습니다.", _logger);
        }
    }

    [SlashCommand("설정", "유저의 재화를 설정합니다.")]
    public async Task SetMoneyAsync(
        [Summary(description: "대상 유저")] IUser targetUser,
        [Summary(description: "돈")] long amount
    )
    {
        try
        {
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

            await _repo.SetPointsAsync(targetUser.Id, (int) amount);
            await RespondAsync($"⚙️ **{targetUser.Username}**의 포인트를 설정했어!", ephemeral: true);
            _logger.LogInformation("{Admin}님이 {Target}님의 포인트를 지급했습니다.", Context.User.Username, targetUser.Username);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("돈관리", ex, "유저의 돈을 관리하는 과정에서 에러가 발생했습니다.", _logger);
        }
    }
}