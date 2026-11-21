using Discord.Interactions;

public class EasterEggCommands(ILogger<EasterEggCommands> logger, Util util) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<EasterEggCommands> _logger = logger;
    private readonly Util _util = util;

    // 유저 행동에 대한 테토의 반응
    public Dictionary<string, string[]> ActionofMove = new() {
        { "인사", [
            "안녕~!",
            "안녕~! 잘 지냈어?",
            "여기, 여기! 토리 여기 있어!"
        ]},

        { "쓰다듬기", [
            "아..앗 뭐하는 거야! 머리 헝클어지잖아...",
            "..... 1분만이야..!",
            "으으... 조금만 더 해도 돼."
        ]},

        { "안아주기", [
            "우와앗?! 갑자기 뭐 하는 거야!",
            "..... 후아... 따뜻해...",
            "으으... 이번만이야! 알았지?",
            "너무 꽉 안으면 안 된다구~!"
        ]},

        { "볼찌르기", [
            "으븝?! 뭐, 뭐야!",
            "볼 찌르지 마아~!",
            "내 볼이 그렇게 신기해?!",
            "한 번만이야! 진짜 한 번만!"
        ]},

        { "하이파이브", [
            "좋아! 하이파이브~!",
            "짝! 헤헤, 재밌다!",
            "이번엔 내가 먼저 간다~!"
        ]},

        { "손잡기", [
            "어...? 손은 왜?",
            "으으... 갑자기 잡으면 놀라잖아!",
            ".... 놓지 마."
        ]},

        { "머리 빗겨주기", [
            "우와, 조심조심 해줘!",
            "헤헤... 기분 좋다~",
            "앗! 거긴 엉켰어!",
            "갑자기 뭐야..."
        ]},

        { "바라보기", [
            "뭐....뭘 바라 봐! 내가 그렇게 이뻐?",
            ".... 왜? 뭐.... 뭐 줄 거 있어..?"
        ]},

        { "빨간미쿠", [
            "빨간 미쿠 **아니거든~!?**",
            "흥! 빨간 미쿠 아니라구~!",
            "하지 마!",
            "으으으... 아니라구!!!!"
        ]}
    };

    [SlashCommand("친해지기", "나...나랑...?!")]
    public async Task FriendAsync(
        [Summary(description: "행동을 선택해줘")] 
        [Choice("인사하기", "인사")] 
        [Choice("쓰담쓰담~", "쓰다듬기")] 
        [Choice("허그~", "안아주기")]
        [Choice("볼 찌르기", "볼찌르기")]
        [Choice("하이파이브~", "하이파이브")]
        [Choice("손 잡기", "손잡기")]
        [Choice("머리 빗겨주기", "빗겨주기")]
        [Choice("바라보기", "바라보기")]
        [Choice("빨간미쿠(하지마!)", "빨간미쿠")]
        string Action)
    {
        try
        {
            if(ActionofMove.TryGetValue(Action, out string[]? a) && a.Length > 0) await RespondAsync(a[Random.Shared.Next(a.Length)]);
            else await RespondAsync("앗, 그 기능은 아직 준비 중이야! 조금만 기다려줘~", ephemeral: true);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("친해지기", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM", _logger);
        }
    }
}