using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Microsoft.Data.Sqlite;
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
    // 봇 전체 공통 에러 응답 문구. 반복되던 문자열을 상수 하나로 정리했다.
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
    /// 모든 명령어의 catch 블록에서 중복되던 로직을 하나로 모았다.
    /// </summary>
    private async Task RespondErrorAsync(string commandName, Exception ex, string userMessage = GenericErrorMessage)
    {
        // 콘솔 + ILogger 이중 기록: 콘솔은 즉시 확인용, ILogger는 호스팅 환경(파일/모니터링 등)과 연동하기 위함.
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
            
            // 💡 이미 Defer 되었으므로 RespondAsync가 아닌 FollowupAsync를 사용합니다.
            await FollowupAsync($"✅ {channel.Mention} 채널에 공지를 전송했어!", ephemeral: true);

            _logger.LogInformation("{User}님이 {Channel} 채널에 공지를 전송했습니다. 제목: {Title}", Context.User.Username, channel.Name, modal.TitleInput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "공지 모달 제출 처리 중 오류 발생");
            await FollowupAsync($"공지 전송 중 오류가 발생했어!\n`{ex.Message}`", ephemeral: true);
        }
    }

    [SlashCommand("명령어등록", "현재 서버에 슬래시 명령어를 수동으로 강제 등록합니다.")]
    public async Task ManualRegisterAsync()
    {
        // 등록 작업이 살짝 걸릴 수 있으므로 응답 대기 상태(Thinking)로 먼저 띄움
        await DeferAsync(ephemeral: true);

        try
        {
            // 1. 현재 서버(Guild)에 즉시 등록 (테스트용으로 가장 추천!)
            await _interactionService.RegisterCommandsToGuildAsync(Context.Guild.Id);

            // 2. 만약 봇이 들어간 모든 곳(전역)에 등록하고 싶다면 아래 코드를 사용 (반영까지 최대 1시간 소요)
            // await _interactionService.RegisterCommandsGloballyAsync();

            await FollowupAsync("✅ 슬래시 명령어가 이 서버에 성공적으로 수동 등록되었습니다!", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ 등록 중 오류가 발생했습니다: `{ex.Message}`", ephemeral: true);
        }
    }   

    [SlashCommand("공지", "모달 팝업 창을 띄워 편하게 서버 공지를 작성합니다.")]
    public async Task AnnounceModalCommand(
        [Summary(description: "공지를 보낼 채널")] ITextChannel channel)
    {
        try
        {
            _logger.LogInformation("{User}님이 '공지' 명령어로 {Channel} 채널에 공지 모달을 요청했습니다.", Context.User.Username, channel.Name);

            // 커스텀 ID에 채널 ID를 숨겨서 전달합니다. (모달 제출 시 어느 채널로 보낼지 복원하기 위함)
            await Context.Interaction.RespondWithModalAsync<AnnounceModal>($"announce_modal_{channel.Id}");
        }
        catch (Exception ex)
        {
            // 💡 발생 상황: 모달 팝업 창을 띄우는 과정에서 디스코드 통신이 끊겼을 때
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
            // 💡 발생 상황: 봇의 역할이 부여하려는 역할보다 아래에 있거나 '역할 관리' 권한이 없을 때
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
            using (var selectCmd = new SqliteCommand(selectQuery, db))
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

            // 3) 신규 유저면 INSERT, 기존 유저면 UPDATE
            if (isNewUser)
            {
                const string insertQuery = "INSERT INTO Users (UserId, Points, LastCheckIn) VALUES (@UserId, @Points, 'None')";
                using var insertCmd = new SqliteCommand(insertQuery, db);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@Points", finalPoints);
                insertCmd.ExecuteNonQuery();
            }
            else
            {
                const string updateQuery = "UPDATE Users SET Points = @Points WHERE UserId = @UserId";
                using var updateCmd = new SqliteCommand(updateQuery, db);
                updateCmd.Parameters.AddWithValue("@Points", finalPoints);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.ExecuteNonQuery();
            }

            string actionText = action == "add" ? "지급" : (action == "remove" ? "차감" : "설정");
            await RespondAsync($"💸 **{targetUser.Username}**님의 포인트를 조정했어!\n> 작업: **{amount:N0} P {actionText}**\n> 현재 잔액: **{finalPoints:N0} P**");

            _logger.LogInformation(
                "{Admin}님이 {Target}님의 포인트를 조정했습니다. 작업={Action}, 액수={Amount}, 최종잔액={FinalPoints}",
                Context.User.Username, targetUser.Username, actionText, amount, finalPoints);
        }
        catch (Exception ex)
        {
            // 💡 발생 상황: DB 접속 실패 또는 SQL 쿼리 실행 에러
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
            // 💡 발생 상황: 봇이 채널에 메시지 전송 권한이나 리액션 추가 권한이 없을 때
            await RespondErrorAsync("투표", ex);
        }
    }
}
