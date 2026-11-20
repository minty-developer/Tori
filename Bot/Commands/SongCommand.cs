using Discord.Interactions;

public class SongCommands (ILogger<SongCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<SongCommands> _logger = logger;

    [SlashCommand("테토리스", "테테테테토테토~")]
    public async Task TetoRisToriCommand()
    {
        try
        {
            await RespondAsync($"테테테 테토테토 테테테 테토리~스\n뭐? 같이 부르고 싶다고?\n누... 누가 같이 불러준대?! (가사: https://minty-developer.github.io/lyrics_bokaro/?id=TT_0006)");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("테토리스", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

    [SlashCommand("메스머라이저", "미쿠쨩~ ㅠㅠㅠ")]
    public async Task Mezumaraiza()
    {
        try
        {
            await RespondAsync("아나타 단단 네무쿠 나루\n아사하카나 메즈마라이즈\n아타마, 카라다, 케무니 마쿠, 마사카 아마타 타부라카스?!\n메노 마에데 유라구 코-카 우고카나쿠 나루 카나다...\n\n어....어....미쿠!!! (가사: https://minty-developer.github.io/lyrics_bokaro/?id=V2_0005)");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("메스머라이저", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

    /// <summary>
    /// 이미 응답했으면 FollowupAsync, 아직이면 RespondAsync로 안전하게 에러 메시지를 보낸다.
    /// 명령어마다 반복되던 에러 처리 로직을 하나로 모았다.
    /// </summary>
    private async Task RespondErrorAsync(string commandName, Exception ex, string userMessage)
    {
        Console.WriteLine($"[{commandName} Error] {ex.Message}");
        _logger.LogError(ex, "[{CommandName}] 명령어 처리 중 오류 발생", commandName);

        if (Context.Interaction.HasResponded)
            await FollowupAsync(userMessage, ephemeral: true);
        else
            await RespondAsync(userMessage, ephemeral: true);
    }
}