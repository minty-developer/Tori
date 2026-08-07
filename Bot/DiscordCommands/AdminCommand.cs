using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using MySqlConnector;
using Microsoft.Extensions.Logging;
using model;

// 1. 팝업 창(Modal) UI 클래스
// '공지' 명령어를 실행하면 유저에게 이 클래스에 정의된 입력 폼이 뜬다.
public class AnnounceModal : IModal
{
    public string Title => "📢 서버 공지 작성하기";

    [InputLabel("공지 제목")]
    [ModalTextInput("announce_title", TextInputStyle.Short, placeholder: "예: 업데이트 안내(v1.0.0)", maxLength: 100)]
    public string TitleInput { get; set; } = string.Empty;

    [InputLabel("1. 주요 내용 (엔터 가능)")]
    [ModalTextInput("announce_main", TextInputStyle.Paragraph, placeholder: "주요 내용을 자유롭게 적어줘!", maxLength: 800)]
    public string MainContent { get; set; } = string.Empty;

    [InputLabel("2. 세부 내용 (선택 사항)")]
    [RequiredInput(false)] 
    [ModalTextInput("announce_detail", TextInputStyle.Paragraph, placeholder: "세부 내용을 적거나 비워둬도 돼!", maxLength: 800)]
    public string DetailContent { get; set; } = string.Empty;
}

// 서버 관리자 전용 명령어 모음 (공지, 역할관리, 돈관리, 투표)
public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
{
    private const string GenericErrorMessage = "봇 에러가 났습니다. \n *점검:*\n- 관리자 DM";

    private readonly DatabaseService _dbService;
    private readonly ILogger<AdminCommands> _logger;
    private readonly InteractionService _interactionService;

    public AdminCommands(DatabaseService dbService, ILogger<AdminCommands> logger, InteractionService interactionService)
    {
        _dbService = dbService;
        _logger = logger;
        _interactionService = interactionService;
    }

    /// <summary>
    /// 이미 응답했으면 FollowupAsync, 아직이면 RespondAsync로 안전하게 에러 메시지를 보낸다.
    /// </summary>
    private async Task RespondErrorAsync(string commandName, Exception ex, string userMessage = GenericErrorMessage)
    {
        Console.WriteLine($"[{commandName} Error] {ex.Message}");
        _logger.LogError(ex, "[{CommandName}] 명령어 처리 중 오류 발생", commandName);

        if (Context.Interaction.HasResponded)
            await FollowupAsync(userMessage, ephemeral: true);
        else
            await RespondAsync(userMessage, ephemeral: true);
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
                formattedMessage = formattedMessage.Substring(0, 1990) + "\n...(후략)";
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

    [SlashCommand("명령어등록", "슬래시 명령어를 갱신하거나 중복 커맨드를 청소합니다.")]
    public async Task ManualRegisterAsync(
        [Summary(description: "작업 선택 (기본값: 전역 갱신 및 청소)")]
        [Choice("전역 갱신 + 길드 중복 청소 (추천)", "global_clean")]
        [Choice("현재 길드 커맨드 전체 삭제 (초기화)", "clean_only")]
        [Choice("현재 길드 전용으로만 재등록", "guild_only")]
        string mode = "global_clean")
    {
        await DeferAsync(ephemeral: true);

        try
        {
            if (mode == "global_clean")
            {
                // 1. 현재 서버의 길드 전용 커맨드(중복 원인)를 전부 싹 지움
                await Context.Guild.DeleteApplicationCommandsAsync();

                // 2. 전역(Global) 커맨드를 다시 갱신 등록
                await _interactionService.RegisterCommandsGloballyAsync();

                await FollowupAsync("✅ 길드 중복 커맨드를 삭제하고 **전역 슬래시 명령어**를 성공적으로 갱신했어!\n*(디스코드 `Ctrl + R`로 새로고침해봐!)*", ephemeral: true);
            }
            else if (mode == "clean_only")
            {
                // 현재 서버에 묶인 커맨드만 싹 청소
                await Context.Guild.DeleteApplicationCommandsAsync();
                await FollowupAsync("🧹 현재 서버에 등록된 길드 전용 명령어를 모두 청소했어!", ephemeral: true);
            }
            else if (mode == "guild_only")
            {
                // 테스트용: 현재 서버에만 즉시 반응하는 길드 커맨드로 강제 등록
                await _interactionService.RegisterCommandsToGuildAsync(Context.Guild.Id);
                await FollowupAsync("⚡ 현재 서버 전용(Guild)으로 슬래시 명령어를 수동 등록했어! (전역 커맨드와 중복될 수 있음)", ephemeral: true);
            }

            _logger.LogInformation("{User}님이 '명령어등록' (모드: {Mode})을 실행했습니다.", Context.User.Username, mode);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ 등록 중 오류가 발생했습니다: `{ex.Message}`", ephemeral: true);
        }
    }

    [SlashCommand("공지", "모달 팝업 창을 띄워 편하게 서버 공지를 작성합니다.")]
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
            await RespondErrorAsync("공지 모달", ex);
        }
    }

    [SlashCommand("역할관리", "특정 유저에게 역할을 부여하거나 빼앗습니다.")]
    public async Task ManageRoleAsync(
        [Summary(description: "작업 종류")] [Choice("부여", "add"), Choice("회수", "remove")] string action,
        [Summary(description: "대상 유저")] SocketGuildUser targetUser,
        [Summary(description: "대상 역할")] SocketRole targetRole)
    {
        try
        {
            bool hasRole = targetUser.Roles.Any(r => r.Id == targetRole.Id);

            if (action == "add")
            {
                if (hasRole)
                {
                    await RespondAsync("이 유저는 이미 그 역할을 가지고 있어!", ephemeral: true);
                    return;
                }

                await targetUser.AddRoleAsync(targetRole);
                await RespondAsync($"✅ **{targetUser.Username}**님에게 **{targetRole.Name}** 역할을 부여했어!", ephemeral: true);
                _logger.LogInformation("{Admin}님이 {Target}님에게 {Role} 역할을 부여했습니다.", Context.User.Username, targetUser.Username, targetRole.Name);
            }
            else
            {
                if (!hasRole)
                {
                    await RespondAsync("이 유저는 그 역할이 없는걸?", ephemeral: true);
                    return;
                }

                await targetUser.RemoveRoleAsync(targetRole);
                await RespondAsync($"✅ **{targetUser.Username}**님의 **{targetRole.Name}** 역할을 회수했어!", ephemeral: true);
                _logger.LogInformation("{Admin}님이 {Target}님의 {Role} 역할을 회수했습니다.", Context.User.Username, targetUser.Username, targetRole.Name);
            }
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("역할관리", ex);
        }
    }

    [SlashCommand("돈관리", "특정 유저의 포인트를 강제로 조정합니다.")]
    public async Task ManageMoneyAsync(
        [Summary(description: "작업 종류")] [Choice("지급 (+)", "add"), Choice("차감 (-)", "remove"), Choice("설정 (=)", "set")] string action,
        [Summary(description: "대상 유저")] IUser targetUser,
        [Summary(description: "포인트 액수")] long amount)
    {
        try
        {
            if (amount < 0)
            {
                await RespondAsync("액수는 0보다 커야 해!", ephemeral: true);
                return;
            }

            long userId = (long)targetUser.Id;
            long currentPoints = 0;
            bool isNewUser = false;

            using var db = _dbService.GetConnection();
            db.Open();

            // 1) 현재 보유 포인트 조회 (신규 유저면 0으로 시작)
            const string selectQuery = "SELECT Points FROM Users WHERE UserId = @UserId";
            using (var selectCmd = new MySqlCommand(selectQuery, db))
            {
                selectCmd.Parameters.AddWithValue("@UserId", userId);

                var result = selectCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    isNewUser = true;
                else
                    currentPoints = Convert.ToInt64(result);
            }

            // 2) 요청된 작업(지급/차감/설정)에 따라 최종 포인트 계산
            long finalPoints = action switch
            {
                "add" => currentPoints + amount,
                "remove" => Math.Max(0, currentPoints - amount),
                "set" => amount,
                _ => currentPoints
            };

            const string upsertQuery = @"
                INSERT INTO Users (UserId, Points, LastCheckIn) 
                VALUES (@UserId, @FinalPoints, NULL)
                ON DUPLICATE KEY UPDATE Points = @FinalPoints;";

            using var cmd = new MySqlCommand(upsertQuery, db);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@FinalPoints", finalPoints);
            cmd.ExecuteNonQuery();

            string actionText = action == "add" ? "지급" : (action == "remove" ? "차감" : "설정");
            await RespondAsync($"💸 **{targetUser.Username}**님의 포인트를 조정했어!\n> 작업: **{amount:N0} P {actionText}**\n> 현재 잔액: **{finalPoints:N0} P**");

            _logger.LogInformation(
                "{Admin}님이 {Target}님의 포인트를 조정했습니다. 작업={Action}, 액수={Amount}, 최종잔액={FinalPoints}",
                Context.User.Username, targetUser.Username, actionText, amount, finalPoints);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("돈관리", ex);
        }
    }

    [SlashCommand("투표", "커스텀 임베드 투표를 엽니다. (옵션은 최대 5개)")]
    public async Task CreatePollAsync(
        [Summary(description: "투표 주제")] string title,
        [Summary(description: "옵션 1")] string option1,
        [Summary(description: "옵션 2")] string option2,
        [Summary(description: "옵션 3 (선택)")] string option3 = "",
        [Summary(description: "옵션 4 (선택)")] string option4 = "",
        [Summary(description: "옵션 5 (선택)")] string option5 = "")
    {
        try
        {
            var options = new List<string> { option1, option2 };
            if (!string.IsNullOrWhiteSpace(option3)) options.Add(option3);
            if (!string.IsNullOrWhiteSpace(option4)) options.Add(option4);
            if (!string.IsNullOrWhiteSpace(option5)) options.Add(option5);

            string[] emojis = { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣" };

            var descriptionBuilder = new System.Text.StringBuilder("아래 이모지를 눌러서 투표해 줘!\n\n");
            for (int i = 0; i < options.Count; i++)
            {
                descriptionBuilder.Append($"{emojis[i]} **{options[i]}**\n\n");
            }

            var embed = new EmbedBuilder()
                .WithTitle($"📊 {title}")
                .WithDescription(descriptionBuilder.ToString())
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .WithFooter($"투표 개설자: {Context.User.Username}", Context.User.GetAvatarUrl())
                .Build();

            await RespondAsync("투표를 생성하는 중...", ephemeral: true);
            var pollMessage = await Context.Channel.SendMessageAsync(embed: embed);

            for (int i = 0; i < options.Count; i++)
            {
                await pollMessage.AddReactionAsync(new Emoji(emojis[i]));
            }

            await FollowupAsync("✅ 투표가 성공적으로 열렸어!", ephemeral: true);

            _logger.LogInformation("{User}님이 '{Title}' 투표를 생성했습니다. 옵션 수={OptionCount}", Context.User.Username, title, options.Count);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("투표", ex);
        }
    }
}