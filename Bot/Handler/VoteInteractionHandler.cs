using Discord;
using Discord.Interactions;

public class VoteInteractionHandler(
    VoteRepository repository,
    PollMessageService pollMessageService,
    ILogger<VoteInteractionHandler> logger
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly VoteRepository _repository = repository;
    private readonly PollMessageService _pollMessageService = pollMessageService;
    private readonly ILogger<VoteInteractionHandler> _logger = logger;

    [ComponentInteraction("poll:*:vote:*")]
    public async Task HandleVoteAsync(
        string pollIdString,
        string optionIdString)
    {
        try
        {
            // ID 검증
            if (!ulong.TryParse(pollIdString, out ulong pollId) ||
                !ulong.TryParse(optionIdString, out ulong optionId))
            {
                await RespondAsync(
                    "잘못된 투표 정보야!",
                    ephemeral: true);

                return;
            }

            // Interaction 응답 예약
            await DeferAsync(ephemeral: true);

            // 투표 등록
            var result = await _repository.RegisterVoteAsync(
                pollId,
                optionId,
                Context.User.Id);

            string response = result switch
            {
                VoteResult.Success =>
                    "✅ 투표가 반영됐어!",

                VoteResult.PollNotActive =>
                    "현재 진행 중인 투표가 아니야!",

                VoteResult.OptionNotFound =>
                    "존재하지 않는 선택지야!",

                VoteResult.AlreadyVoted =>
                    "이미 투표했어! 이 투표에서는 재투표할 수 없어.",

                _ =>
                    "투표 처리 중 예상하지 못한 결과가 발생했어!"
            };

            if (result == VoteResult.Success)
            {
                try
                {
                    await _pollMessageService.UpdatePollMessageAsync(pollId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "투표 메시지 갱신에 실패했습니다. PollId={PollId}",
                        pollId);
                }
            }

            // 사용자에게 결과 전달
            await FollowupAsync(
                response,
                ephemeral: true);

            // 성공 로그
            if (result == VoteResult.Success)
            {
                _logger.LogInformation(
                    "{User}님이 투표했습니다. PollId={PollId}, OptionId={OptionId}",
                    Context.User.Username,
                    pollId,
                    optionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "투표 버튼 처리 중 오류가 발생했습니다.");

            try
            {
                if (!Context.Interaction.HasResponded)
                {
                    await RespondAsync(
                        "투표를 처리하는 중 오류가 발생했어!",
                        ephemeral: true);
                }
                else
                {
                    await FollowupAsync(
                        "투표를 처리하는 중 오류가 발생했어!",
                        ephemeral: true);
                }
            }
            catch (Exception responseException)
            {
                _logger.LogError(
                    responseException,
                    "투표 오류 응답 전송에 실패했습니다.");
            }
        }
    }
}