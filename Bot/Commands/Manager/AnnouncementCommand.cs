using Discord.Interactions;
using Discord.WebSocket;

[Group("공지", "서버 공지를 관리합니다.")]
public class AnnounceCommands(
    ILogger<AnnounceCommands> logger,
    Util util
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<AnnounceCommands> _logger = logger;
    private readonly Util _util = util;

    [SlashCommand("작성", "모달 팝업 창을 띄워 편하게 서버 공지를 작성합니다.")]
    public async Task AnnounceModalCommand()
    {
        try
        {
            _logger.LogInformation("{User}님이 '공지' 명령어로 공지-봇 채널에 공지 모달을 요청했습니다.", Context.User.Username);

            long channelId = BotEnv.isDev ? 1479322191148613686 : 1529006865260740729;
            
            // AnnounceModal을 직접 넘기는 대신 RespondWithModalAsync 내장 래퍼 형식에 맞춰 호출
            await Context.Interaction.RespondWithModalAsync<AnnounceModal>($"announce_modal_{channelId}");
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("공지 모달", ex, "공지 모달을 켜는 과정에서 에러가 났습니다.", _logger);
        }
    }

    // 유저가 모달에서 '제출'을 누르면 실행되는 메서드
    [ModalInteraction("announce_modal_*")]
    public async Task HandleAnnounceModal(string channelIdStr, AnnounceModal modal)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            string customId = (Context.Interaction as SocketModal)?.Data.CustomId ?? "";

            if (!ulong.TryParse(channelIdStr, out ulong channelId))
            {
                await FollowupAsync("채널 정보를 찾지 못했어!", ephemeral: true);
                return;
            }

            var channel = Context.Guild?.GetTextChannel(channelId);
            if (channel == null)
            {
                await FollowupAsync("공지를 보낼 채널을 찾을 수 없어!", ephemeral: true);
                return;
            }

            string today = DateTime.UtcNow.AddHours(9).ToString("yyyy년 MM월 dd일 (ddd)");

            string mainContent = string.IsNullOrWhiteSpace(modal.MainContent) ? "내용 없음" : modal.MainContent;
            string detailContent = string.IsNullOrWhiteSpace(modal.DetailContent) ? "없음" : modal.DetailContent;

            string formattedMessage = $@"공지대상: @everyone 

    ━━━━━━━ 💿 **[서버 공지사항]** 💿 ━━━━━━━

    안녕하세요, **보카로** 관리진입니다!
    서버원 분들이 즐겁고 쾌적하게 즐길 수 있도록 몇 가지 안내를 전달드립니다.

    ### 📢 [공지 제목: {modal.TitleInput}]

    **1. 주요 내용**
    {mainContent}

    **2. 세부 내용**
    {detailContent}

    **3. 변경 및 적용 일시**
    * **일시:** 공지시({today})부터 즉시 적용

    **4. 관리진 한마디**
    > 💬 ""언제나 보컬로이드들을 사랑해 주시는 서버원분들께 감사드립니다. 서로 존중하며 즐거운 덕질 공간을 만들어 가요!""

    💡 **문의 사항이 있다면?**
    궁금한 점이나 건의사항은 #문의-및-건의 채널 혹은 @관리자 에게 DM을 보내주세요.

    ━━━━━━━━━━━━━━━━━━━━━━━━━━

    쓴 이: {Context.User.Mention}  |  전송: <:not_redmiku:1510473675382460616> 토리 봇  |  수정 상태: ❌";

            if (formattedMessage.Length > 2000)
            {
                formattedMessage = formattedMessage[..1990] + "\n...(후략)";
            }

            await channel.SendMessageAsync(formattedMessage);
            
            await FollowupAsync($"✅ {channel.Mention} 채널에 공지를 전송했어!", ephemeral: true);

            _logger.LogInformation("{User}님이 {Channel} 채널에 공지를 전송했습니다. 제목: {Title}", Context.User.Username, channel.Name, modal.TitleInput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "공지 모달 제출 처리 중 오류 발생");
            await FollowupAsync($"공지 전송 중 오류가 발생했어!\n`{ex.Message}`", ephemeral: true);
        }
    }
}