using Discord.Interactions;

public class EasterEggCommands(ILogger<EasterEggCommands> logger, Util util) : InteractionModuleBase<SocketInteractionContext>
{
    // 유저 행동에 대한 테토의 반응
    public Dictionary<string, string[]> ActionofMove = new() {
        { "인사", ["안녕~!", "안녕~! 잘 지냈어?", "여기, 여기! 토리 여기 있어!"] },
        { "쓰다듬기", ["아..앗 뭐하는 거야! 머리 헝클어지잖아...", "..... 1분만이야!"] },
        { "바라보기", ["뭐....뭘 바라 봐! 내가 그렇게 이뻐?", ".... 왜? 뭐.... 뭐 줄 거 있어..?"] },
        { "빨간미쿠", ["빨간 미쿠 **아니거든~!?**", "흥! 빨간 미쿠 아니라구~!", "하지 마!"] }
    };

    private readonly ILogger<EasterEggCommands> _logger = logger;
    private readonly Util _util = util;

    [SlashCommand("빨간미쿠", "하지 마!")]
    public async Task RedMikuToriCommand()
    {
        try
        {
            await RespondAsync();
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("빨간미쿠", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM", _logger);
        }
    }

    [SlashCommand("친해지기", "나...나랑...?!")]
    public async Task FriendAsync(
        [Summary(description: "행동을 선택해줘")] 
        [Choice("인사하기", "인사")] 
        [Choice("쓰담쓰담~", "쓰다듬기")] 
        [Choice("바라보기", "바라보기")]
        [Choice("빨간미쿠(하지마!)", "빨간미쿠")]
        string Action)
    {
        try
        {
            if(ActionofMove.TryGetValue(Action, out string[]? a)) await RespondAsync(a[Random.Shared.Next(a.Length)]);
            else await RespondAsync("앗, 그 기능은 아직 준비 중이야! 조금만 기다려줘~", ephemeral: true);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("친해지기", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM", _logger);
        }
    }
}