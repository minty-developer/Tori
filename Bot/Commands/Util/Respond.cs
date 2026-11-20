using Discord.Interactions;

public class Util : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    /// 이미 응답했으면 FollowupAsync, 아직이면 RespondAsync로 안전하게 에러 메시지를 보내는 Helper 메서드
    /// </summary>
    public async Task RespondErrorAsync<T>(string commandName, Exception ex, string userMessage, ILogger<T> _logger)
    {
        Console.WriteLine($"[{commandName} Error] {ex.Message}");
        _logger.LogError(ex, "[{CommandName}] 명령어 처리 중 오류 발생", commandName);

        if (Context.Interaction.HasResponded)
            await FollowupAsync(userMessage, ephemeral: true);
        else
            await RespondAsync(userMessage, ephemeral: true);
    }
}