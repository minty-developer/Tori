using Discord;
using Discord.Interactions;

public class TitleCommands(
    TitleRepository titleRepository,
    ILogger<TitleCommands> logger,
    Util util
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TitleRepository _repo = titleRepository;
    private readonly ILogger<TitleCommands> _logger = logger;
    private readonly Util _util = util;

    // 상점
    [SlashCommand("상점", "골라골라 맘껏 골라~")]
    public async Task ShowShopAsync()
    {
        try
        {
            await DeferAsync();

            var titles = await _repo.GetShopTitlesAsync();

            if (titles.Count == 0)
            {
                await FollowupAsync("우리 재고 없어!");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("🛍️ [토리의 칭호 상점]")
                .WithDescription(
                    "모은 포인트로 멋진 칭호를 구매해봐!\n" +
                    "구매는 `/칭호구매 <칭호 코드>`로 할 수 있어.")
                .WithColor(Color.Gold);

            foreach (var title in titles)
            {
                embed.AddField(
                    $"{title.Name} (코드: `{title.Code}`)",
                    $"설명: {title.Description}\n" +
                    $"가격: **{title.Price:N0} P**",
                    false);
            }

            await FollowupAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "상점",
                ex,
                "상점 데이터를 불러오지 못했습니다.\n" +
                "*점검:*\n- DB 연결\n- 관리자 DM",
                _logger);
        }
    }

    // 칭호 구매
    [SlashCommand("칭호구매", "드디어... 손에 넣다니...!")]
    public async Task BuyTitleAsync(
        [Summary(description: "상점에 적힌 칭호 코드 (예: MorningBird)")]
        string titleCode)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            var title = await _repo.GetTitleAsync(titleCode);

            if (title is null)
            {
                await FollowupAsync(
                    "그런 코드를 가진 칭호는 상점에 없는데!",
                    ephemeral: true);
                return;
            }

            var result = await _repo.PurchaseTitleAsync(
                Context.User.Id,
                titleCode);

            switch (result.Status)
            {
                case PurchaseStatus.Success:
                    await FollowupAsync(
                        $"🎉 축하해! **{title.Name}** 칭호를 구매했어!\n" +
                        $"잔여 포인트: **{result.RemainingPoints:N0} P**\n" +
                        $"장착은 `/칭호장착 {titleCode}`로 해봐!",
                        ephemeral: true);

                    _logger.LogInformation(
                        "{User}님이 '{TitleCode}' 칭호를 구매했습니다. 잔여 포인트={RemainingPoints}",
                        Context.User.Username,
                        titleCode,
                        result.RemainingPoints);
                    break;

                case PurchaseStatus.UserNotFound:
                    await FollowupAsync(
                        "아직 가입(출석체크)을 안 해서 포인트가 없어! `/출첵`부터 해봐!",
                        ephemeral: true);
                    break;

                case PurchaseStatus.TitleNotFound:
                    await FollowupAsync(
                        "그런 칭호는 상점에 없는데?",
                        ephemeral: true);
                    break;

                case PurchaseStatus.AlreadyOwned:
                    await FollowupAsync(
                        "너 그거 이미 가지고 있잖아!",
                        ephemeral: true);
                    break;

                case PurchaseStatus.InsufficientPoints:
                    await FollowupAsync(
                        $"포인트가 부족해!\n" +
                        $"현재: **{result.RemainingPoints:N0} P** / " +
                        $"필요: **{title.Price:N0} P**",
                        ephemeral: true);
                    break;

                case PurchaseStatus.NotForSale:
                    await FollowupAsync(
                        "이 칭호는 돈 주고 살 수 있는 게 아니야!",
                        ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "칭호구매",
                ex,
                "상점이 어쩔 수 없이 잠시 쉬는 중입니다!\n" +
                "*DB SQL 처리에 실패했습니다.*\n" +
                "*점검:*\n- DB 연결\n- 관리자 DM",
                _logger);
        }
    }

    // 칭호 장착
    [SlashCommand("칭호장착", "반짝 반짝!")]
    public async Task EquipTitleAsync(
        [Summary(description: "장착할 칭호 코드 (예: MorningBird)")]
        string titleCode)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            var title = await _repo.GetTitleAsync(titleCode);

            if (title is null)
            {
                await FollowupAsync(
                    "그런 칭호는 존재하지 않아!",
                    ephemeral: true);
                return;
            }

            bool success = await _repo.EquipTitleAsync(
                Context.User.Id,
                titleCode);

            if (!success)
            {
                await FollowupAsync(
                    "너 그 칭호 안 가지고 있는데... 도둑질은 나쁜 거야!",
                    ephemeral: true);
                return;
            }

            await FollowupAsync(
                $"✨ 칭호 장착 완료! 이제부터 네 프로필에 " +
                $"**{title.Name}**(이)가 반짝일 거야!",
                ephemeral: true);

            _logger.LogInformation(
                "{User}님이 '{TitleCode}' 칭호를 장착했습니다.",
                Context.User.Username,
                titleCode);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "칭호장착",
                ex,
                "칭호에 적응하지 못했습니다!\n" +
                "*DB SQL 처리에 실패했습니다.*\n" +
                "*점검:*\n- DB 연결\n- 관리자 DM",
                _logger);
        }
    }
}