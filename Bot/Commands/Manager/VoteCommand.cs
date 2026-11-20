using Discord;
using Discord.Interactions;

[Group("투표", "서버 투표를 관리합니다.")]
public class VoteCommands(
    VoteRepository repository,
    PollMessageService pollMessageService,
    ILogger<VoteCommands> logger,
    Util util
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly VoteRepository _repository = repository;
    private readonly PollMessageService _pollMessageService = pollMessageService;
    private readonly ILogger<VoteCommands> _logger = logger;
    private readonly Util _util = util;

    // 모든 명령어에서 사용하는 관리자 권한 검사
    private async Task<bool> CheckAdminPermissionAsync()
    {
        if (Context.User is not IGuildUser user)
        {
            await RespondAsync(
                "서버에서만 사용할 수 있는 명령어야!",
                ephemeral: true);

            return false;
        }

        if (!user.GuildPermissions.Administrator)
        {
            await RespondAsync(
                "관리자만 사용할 수 있는 명령어야!",
                ephemeral: true);

            return false;
        }

        return true;
    }

    // 투표 생성
    [SlashCommand("생성", "새로운 투표를 생성합니다.")]
    public async Task CreatePollAsync(
        [Summary(description: "투표 주제")] string title,
        [Summary(description: "선택지 ($$로 구분)")] string options,
        [Summary(description: "익명 투표 여부")] bool isAnonymous = true,
        [Summary(description: "재투표 허용 여부")] bool allowRevote = false)
    {
        try
        {
            if (!await CheckAdminPermissionAsync())
                return;

            var optionList = options
                .Split("$$", StringSplitOptions.TrimEntries)
                .ToList();

            // 입력값 검증
            if (string.IsNullOrWhiteSpace(title))
            {
                await RespondAsync(
                    "투표 주제를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            if (optionList.Count < 2 || optionList.Count > 5)
            {
                await RespondAsync(
                    "선택지는 최소 2개, 최대 5개까지 입력할 수 있어!",
                    ephemeral: true);

                return;
            }

            if (optionList.Any(string.IsNullOrWhiteSpace))
            {
                await RespondAsync(
                    "빈 선택지는 사용할 수 없어!",
                    ephemeral: true);

                return;
            }

            if (optionList.Any(option => option.Length > 100))
            {
                await RespondAsync(
                    "각 선택지는 100자 이내로 입력해 줘!",
                    ephemeral: true);

                return;
            }

            await DeferAsync(ephemeral: true);

            // 투표 생성
            ulong pollId = await _repository.CreatePollAsync(
                Context.User.Id,
                title,
                optionList,
                isAnonymous,
                allowRevote);

            await FollowupAsync(
                $"✅ 투표 초안이 생성됐어!\n" +
                $"> 투표 번호: **#{pollId:D4}**\n" +
                $"> 주제: **{title}**\n" +
                $"> 선택지 수: **{optionList.Count}개**\n" +
                $"> 익명 여부: **{(isAnonymous ? "익명" : "공개")}**\n" +
                $"> 재투표: **{(allowRevote ? "허용" : "불가")}**\n\n" +
                $"`/투표 수정` 또는 `/투표 시작`으로 관리할 수 있어.",
                ephemeral: true);

            _logger.LogInformation(
                "{User}님이 투표 초안을 생성했습니다. PollId={PollId}, Title={Title}, OptionCount={OptionCount}, IsAnonymous={IsAnonymous}, AllowRevote={AllowRevote}",
                Context.User.Username,
                pollId,
                title,
                optionList.Count,
                isAnonymous,
                allowRevote);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "투표",
                ex,
                "투표를 생성하는 과정에서 오류가 발생했습니다.",
                _logger);
        }
    }

    // 투표 초안 수정
    [SlashCommand("수정", "생성한 투표 초안을 수정합니다.")]
    public async Task UpdatePollAsync(
        [Summary(description: "투표 번호")] long pollId,
        [Summary(description: "수정할 투표 주제")] string title,
        [Summary(description: "수정할 선택지 ($$로 구분)")] string options)
    {
        try
        {
            if (!await CheckAdminPermissionAsync())
                return;

            if (pollId <= 0)
            {
                await RespondAsync(
                    "올바른 투표 번호를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            var optionList = options
                .Split("$$", StringSplitOptions.TrimEntries);

            // 입력값 검증
            if (string.IsNullOrWhiteSpace(title))
            {
                await RespondAsync(
                    "투표 주제를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            if (optionList.Length < 2 || optionList.Length > 5)
            {
                await RespondAsync(
                    "선택지는 최소 2개, 최대 5개까지 입력할 수 있어!",
                    ephemeral: true);

                return;
            }

            if (optionList.Any(string.IsNullOrWhiteSpace))
            {
                await RespondAsync(
                    "빈 선택지는 사용할 수 없어!",
                    ephemeral: true);

                return;
            }

            if (optionList.Any(option => option.Length > 100))
            {
                await RespondAsync(
                    "각 선택지는 100자 이내로 입력해 줘!",
                    ephemeral: true);

                return;
            }

            await DeferAsync(ephemeral: true);

            bool success = await _repository.UpdateDraftPollAsync(
                (ulong)pollId,
                title,
                [.. optionList]);

            if (!success)
            {
                await FollowupAsync(
                    "❌ 투표를 수정할 수 없어!\n투표가 존재하지 않거나, 수정할 수 없는 상태일 수 있어.",
                    ephemeral: true);

                return;
            }

            await FollowupAsync(
                $"✅ 투표 초안을 수정했어!\n" +
                $"> 투표 번호: **#{pollId:D4}**\n" +
                $"> 주제: **{title}**\n" +
                $"> 선택지 수: **{optionList.Length}개**",
                ephemeral: true);

            _logger.LogInformation(
                "{User}님이 투표 초안을 수정했습니다. PollId={PollId}",
                Context.User.Username,
                pollId);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "투표",
                ex,
                "투표를 수정하는 과정에서 오류가 발생했습니다.",
                _logger);
        }
    }

    // 투표 시작
    [SlashCommand("시작", "투표를 시작합니다.")]
    public async Task StartPollAsync(
        [Summary(description: "시작할 투표 번호")] long pollId)
    {
        try
        {
            if (!await CheckAdminPermissionAsync())
                return;

            if (pollId <= 0)
            {
                await RespondAsync(
                    "올바른 투표 번호를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            var poll = await _repository.GetPollAsync((ulong)pollId);

            if (poll is null)
            {
                await RespondAsync(
                    "해당 투표를 찾을 수 없어!",
                    ephemeral: true);

                return;
            }

            if (poll.Status != PollStatus.Draft)
            {
                await RespondAsync(
                    "초안 상태인 투표만 시작할 수 있어!",
                    ephemeral: true);

                return;
            }

            await DeferAsync(ephemeral: true);

            // 1. 초안 상태의 투표 메시지 생성 및 DB 저장
            var message = await _pollMessageService.CreatePollMessageAsync(
                poll,
                Context.Channel);

            // 2. DB의 투표 상태 변경
            bool statusUpdated = await _repository.UpdatePollStatusAsync(
                (ulong)pollId,
                PollStatus.Active);

            if (!statusUpdated)
            {
                await message.DeleteAsync();

                await FollowupAsync(
                    "투표 상태가 변경되어 시작할 수 없어!",
                    ephemeral: true);

                return;
            }

            // 3. 투표 메시지 갱신 (버튼 활성화)
            await _pollMessageService.UpdatePollMessageAsync(
                (ulong)pollId);

            // 4. 결과 응답
            await FollowupAsync(
                $"📊 **#{pollId:D4}** 투표가 시작됐어!",
                ephemeral: true);

            _logger.LogInformation(
                "{User}님이 투표를 시작했습니다. PollId={PollId}",
                Context.User.Username,
                pollId);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "투표",
                ex,
                "투표를 시작하는 과정에서 오류가 발생했습니다.",
                _logger);
        }
    }

    // 투표 일시정지
    [SlashCommand("일시정지", "진행 중인 투표를 일시정지합니다.")]
    public async Task PausePollAsync(
        [Summary(description: "투표 번호")] long pollId)
    {
        try
        {
            if (!await CheckAdminPermissionAsync())
                return;

            if (pollId <= 0)
            {
                await RespondAsync(
                    "올바른 투표 번호를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            var poll = await _repository.GetPollAsync((ulong)pollId);

            if (poll is null)
            {
                await RespondAsync(
                    "해당 투표를 찾을 수 없어!",
                    ephemeral: true);

                return;
            }

            if (poll.Status != PollStatus.Active)
            {
                await RespondAsync(
                    "진행 중인 투표만 일시정지할 수 있어!",
                    ephemeral: true);

                return;
            }

            await DeferAsync(ephemeral: true);

            // 1. 투표 상태 변경
            bool success = await _repository.UpdatePollStatusAsync(
                (ulong)pollId,
                PollStatus.Paused);

            if (!success)
            {
                await FollowupAsync(
                    "투표 상태를 변경하지 못했어!",
                    ephemeral: true);

                return;
            }

            // 2. 투표 메시지 갱신
            await _pollMessageService.UpdatePollMessageAsync(
                (ulong)pollId);

            await FollowupAsync(
                $"⏸️ **#{pollId:D4}** 투표를 일시정지했어!",
                ephemeral: true);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "투표",
                ex,
                "투표를 일시정지하는 과정에서 오류가 발생했습니다.",
                _logger);
        }
    }

    // 투표 재개
    [SlashCommand("재개", "일시정지된 투표를 다시 시작합니다.")]
    public async Task ResumePollAsync(
        [Summary(description: "투표 번호")] long pollId)
    {
        try
        {
            if (!await CheckAdminPermissionAsync())
                return;

            if (pollId <= 0)
            {
                await RespondAsync(
                    "올바른 투표 번호를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            var poll = await _repository.GetPollAsync((ulong)pollId);

            if (poll is null)
            {
                await RespondAsync(
                    "해당 투표를 찾을 수 없어!",
                    ephemeral: true);

                return;
            }

            if (poll.Status != PollStatus.Paused)
            {
                await RespondAsync(
                    "일시정지된 투표만 재개할 수 있어!",
                    ephemeral: true);

                return;
            }

            await DeferAsync(ephemeral: true);

            // 1. 투표 상태 변경
            bool success = await _repository.UpdatePollStatusAsync(
                (ulong)pollId,
                PollStatus.Active);

            if (!success)
            {
                await FollowupAsync(
                    "투표 상태를 변경하지 못했어!",
                    ephemeral: true);

                return;
            }

            // 2. 투표 메시지 갱신
            await _pollMessageService.UpdatePollMessageAsync(
                (ulong)pollId);

            await FollowupAsync(
                $"▶️ **#{pollId:D4}** 투표를 재개했어!",
                ephemeral: true);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "투표",
                ex,
                "투표를 재개하는 과정에서 오류가 발생했습니다.",
                _logger);
        }
    }

    // 투표 종료
    [SlashCommand("종료", "진행 중이거나 일시정지된 투표를 종료합니다.")]
    public async Task EndPollAsync(
        [Summary(description: "투표 번호")] long pollId)
    {
        try
        {
            if (!await CheckAdminPermissionAsync())
                return;

            if (pollId <= 0)
            {
                await RespondAsync(
                    "올바른 투표 번호를 입력해 줘!",
                    ephemeral: true);

                return;
            }

            var poll = await _repository.GetPollAsync((ulong)pollId);

            if (poll is null)
            {
                await RespondAsync(
                    "해당 투표를 찾을 수 없어!",
                    ephemeral: true);

                return;
            }

            if (poll.Status != PollStatus.Active &&
                poll.Status != PollStatus.Paused)
            {
                await RespondAsync(
                    "진행 중이거나 일시정지된 투표만 종료할 수 있어!",
                    ephemeral: true);

                return;
            }

            await DeferAsync(ephemeral: true);

            // 1. 투표 상태 변경
            bool success = await _repository.UpdatePollStatusAsync(
                (ulong)pollId,
                PollStatus.Ended);

            if (!success)
            {
                await FollowupAsync(
                    "투표 상태를 변경하지 못했어!",
                    ephemeral: true);

                return;
            }

            // 2. 최종 결과를 포함한 투표 메시지 갱신
            await _pollMessageService.UpdatePollMessageAsync(
                (ulong)pollId);

            await FollowupAsync(
                $"🏁 **#{pollId:D4}** 투표가 종료됐어!",
                ephemeral: true);

            _logger.LogInformation(
                "{User}님이 투표를 종료했습니다. PollId={PollId}",
                Context.User.Username,
                pollId);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "투표",
                ex,
                "투표를 종료하는 과정에서 오류가 발생했습니다.",
                _logger);
        }
    }
}