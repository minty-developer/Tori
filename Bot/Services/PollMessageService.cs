using Discord;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Text;

public class PollMessageService(
    VoteRepository repository,
    DiscordSocketClient client,
    ILogger<PollMessageService> logger)
{
    private readonly VoteRepository _repository = repository;
    private readonly DiscordSocketClient _client = client;
    private readonly ILogger<PollMessageService> _logger = logger;

    // 투표별 메시지 갱신 동시 실행 방지
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim>
        _updateLocks = new();


    // ==================================================
    // Embed
    // ==================================================

    private static Embed CreatePollEmbed(PollInfo poll)
    {
        var description = new StringBuilder();

        string statusText = poll.Status switch
        {
            PollStatus.Draft => "📝 투표 대기 중",
            PollStatus.Active => "🟢 투표 진행 중",
            PollStatus.Paused => "🟠 투표 일시정지",
            PollStatus.Ended => "🔴 투표 종료",
            _ => "알 수 없음"
        };

        description.AppendLine($"**상태:** {statusText}");
        description.AppendLine();

        description.AppendLine(
            poll.IsAnonymous
                ? "🔒 익명 투표"
                : "👥 공개 투표");

        description.AppendLine(
            poll.AllowRevote
                ? "🔄 재투표 허용"
                : "🚫 재투표 불가");

        description.AppendLine();

        switch (poll.Status)
        {
            case PollStatus.Active:
                description.AppendLine("아래 버튼을 눌러 투표해 줘!");
                break;

            case PollStatus.Paused:
                description.AppendLine("투표가 일시정지됐어.");
                break;

            case PollStatus.Ended:
                description.AppendLine("투표가 종료됐어! 최종 결과를 확인해 봐.");
                break;

            default:
                description.AppendLine("아직 시작되지 않은 투표야.");
                break;
        }

        description.AppendLine();

        foreach (var option in poll.Options)
        {
            description.AppendLine(
                $"**{option.Position}.** {option.OptionText} — {option.VoteCount}표");
        }

        var color = poll.Status switch
        {
            PollStatus.Draft => Color.LightGrey,
            PollStatus.Active => Color.Green,
            PollStatus.Paused => Color.Orange,
            PollStatus.Ended => Color.Red,
            _ => Color.Default
        };

        return new EmbedBuilder()
            .WithTitle($"📊 {poll.Title}")
            .WithDescription(description.ToString())
            .WithColor(color)
            .WithFooter($"Tori Poll System | #{poll.PollId:D4}")
            .WithCurrentTimestamp()
            .Build();
    }


    // ==================================================
    // Buttons
    // ==================================================

    private static MessageComponent CreatePollButtons(PollInfo poll)
    {
        var builder = new ComponentBuilder();

        string[] emojis =
        [
            "1️⃣",
            "2️⃣",
            "3️⃣",
            "4️⃣",
            "5️⃣"
        ];

        bool disabled = poll.Status != PollStatus.Active;

        foreach (var option in poll.Options)
        {
            if (option.Position < 1 || option.Position > emojis.Length)
                throw new InvalidOperationException(
                    "투표 선택지의 순서가 올바르지 않습니다.");

            string label = option.OptionText.Length > 80
                ? option.OptionText[..77] + "..."
                : option.OptionText;

            builder.WithButton(
                label: label,
                customId: $"poll:{poll.PollId}:vote:{option.OptionId}",
                style: ButtonStyle.Primary,
                emote: new Emoji(emojis[option.Position - 1]),
                disabled: disabled);
        }

        return builder.Build();
    }


    // ==================================================
    // Update
    // ==================================================

    public async Task UpdatePollMessageAsync(ulong pollId)
    {
        var updateLock = _updateLocks.GetOrAdd(
            pollId,
            _ => new SemaphoreSlim(1, 1));

        await updateLock.WaitAsync();

        try
        {
            // 최신 투표 정보 조회
            var poll = await _repository.GetPollAsync(pollId);

            if (poll is null)
                throw new InvalidOperationException(
                    "투표 정보를 찾을 수 없어!");

            if (poll.ChannelId is null || poll.MessageId is null)
                throw new InvalidOperationException(
                    "투표 메시지 정보가 없어!");

            if (_client.GetChannel(poll.ChannelId.Value)
                is not IMessageChannel channel)
            {
                throw new InvalidOperationException(
                    "투표 채널을 찾을 수 없어!");
            }

            var message = await channel.GetMessageAsync(
                poll.MessageId.Value);

            if (message is not IUserMessage userMessage)
                throw new InvalidOperationException(
                    "투표 메시지를 찾을 수 없어!");

            var embed = CreatePollEmbed(poll);
            var buttons = CreatePollButtons(poll);

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = embed;
                properties.Components = buttons;
            });

            _logger.LogInformation(
                "투표 메시지가 갱신되었습니다. PollId={PollId}",
                pollId);
        }
        finally
        {
            updateLock.Release();
        }
    }

    // 투표 메시지 생성
    public async Task<IUserMessage> CreatePollMessageAsync(
        PollInfo poll,
        IMessageChannel channel)
    {
        // 초안 상태의 Embed 및 버튼 생성
        var embed = CreatePollEmbed(poll);
        var buttons = CreatePollButtons(poll);

        // Discord 메시지 전송
        var message = await channel.SendMessageAsync(
            embed: embed,
            components: buttons);

        try
        {
            // 메시지 정보 DB 저장
            bool success = await _repository.SetPollMessageAsync(
                poll.PollId,
                channel.Id,
                message.Id);

            if (!success)
            {
                throw new InvalidOperationException(
                    "투표 메시지 정보를 저장하지 못했습니다.");
            }

            return message;
        }
        catch
        {
            // DB 저장 실패 시 생성한 메시지 삭제
            await message.DeleteAsync();
            throw;
        }
    }
}