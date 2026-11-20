using System.Text.Json;
using Discord.Interactions;

public class InformationCommands(InformationRepository informationRepository, ILogger<InformationCommands> logger, Util util) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<InformationCommands> _logger = logger;
    private readonly Util _util = util;
    private readonly InformationRepository _repo = informationRepository;

    [SlashCommand("타임라인", "지금까지 우리 서버는...")]
    public async Task ShowServerTimelineAsync()
    {
        try
        {
            var timeline = await _repo.GetTimelineAsync();

            if (timeline.Count == 0)
            {
                await RespondAsync("아직 역사가 없어! 새출발!");
                return;
            }

            var timelineListText = string.Join(
                "\n\n",
                timeline.Select(item =>
                    $"📅 **{item.OccurredAt:yyyy-MM-dd}** - {item.EventType}\n" +
                    $"> {item.Description}"));

            string message =
                $"📜 **[토리의 서버 역사관]** 우리 서버가 걸어온 길이야!\n\n" +
                timelineListText;

            await RespondAsync(message);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "타임라인",
                ex,
                "역사관 보물들이 서로 섞여버렸습니다!\n" +
                "*DB에서 타임라인을 불러오다가 실패했습니다.*\n" +
                "*점검:*\n- 관리자 DM",
                _logger);
        }
    }

    [SlashCommand("웹사이트", "토리의 웹사이트")]
    public async Task ShowWebSiteUrlAsync()
    {
        try
        {
            await RespondAsync("웹사이트는 업데이트 중이야! 잠시만 기다리라구~");
        } catch (Exception ex)
        {
            await _util.RespondErrorAsync("웹사이트", ex, "웹사이트 링크를 보내는 중에 오류가 났습니다!", _logger);
        }
    }

    [SlashCommand("비밀키", "Api키 알아보기")]
    public async Task ShowApiKeyAsync()
    {
        try
        {
            await RespondAsync($"내 키는 여기있긴 한데... 이상하게 쓸 건 아니지?\n||{Environment.GetEnvironmentVariable("TORI_API_KEY")}||");
        } catch (Exception ex)
        {
            await _util.RespondErrorAsync("비밀키", ex, "키를 보여주는 중에 오류가 났습니다!", _logger);
        }
    }

    [SlashCommand("버전", "토리는 얼마나 성장했어?")]
    public async Task ShowVersionAsync()
    {
        try
        {
            string[] Ver = BotEnv.botVersion.Split(".");
            await RespondAsync($"나는 {Ver[1]}번 학습하고 {Ver[2]}번 고쳐졌어!");
        } catch (Exception ex)
        {
            await _util.RespondErrorAsync("버전", ex, "버전을 보여주는 중 오류가 났습니다!", _logger);
        }
    }
}