using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using MySqlConnector;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using model;

// 일반 유저용 명령어 모음 (인사, 출석체크, 포인트, 상점, 도박, 퀴즈, 낚시 등)
public class SlashCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<SlashCommands> _logger;
    private readonly InteractionService _interactionService;

    public SlashCommands(DatabaseService dbService, ILogger<SlashCommands> logger, InteractionService interactionService)
    {
        _dbService = dbService;
        _logger = logger;
        _interactionService = interactionService;
    }

    // 낚시로 등장 가능한 캐릭터 후보 목록.
    // 낚시로 등장 가능한 캐릭터 후보 목록 (밸런스 패치 적용)
    private static readonly List<(string Name, string Grade, long Price)> FishingCharacterPool = new()
    {
        // 일반 등급 (지원금 50~150 P) -> 미끼값(500P) 대비 손해
        ("즌다몬", "일반", 50),
        ("카이토", "일반", 50),
        ("유키", "일반", 70),
        ("카후", "일반", 70),
        ("우이", "일반", 100),
        ("유카", "일반", 100),
        ("린", "일반", 120),
        ("렌", "일반", 120),
        ("네루", "일반", 150),
        ("레이", "일반", 150),

        // 희귀 등급 (지원금 300~500 P) -> 거의 본전~소폭 손해
        ("테토 (한국어)", "희귀", 300),
        ("미쿠 (한국어)", "희귀", 350),
        ("테토 (영어)", "희귀", 400),
        ("미쿠 (영어)", "희귀", 500),

        // 전설 등급 (지원금 1000~2000 P) -> 잭팟 (2% 확률)
        ("테토 (일본어)", "전설", 1000),
        ("미쿠 (일본어)", "전설", 1500),
        ("작곡 (전설의 마스터)", "전설", 2000)
    };

    // 유저 행동에 대한 테토의 반응
    public Dictionary<string, string> ActionofMove = new Dictionary<string, string>() {
        { "인사", "안녕~!" },
        { "쓰다듬기", "아..앗 뭐하는 거야! 머리 헝클어지잖아..."},
        { "바라보기", "뭐....뭘 바라 봐! 내가 그렇게 이뻐?"}
    };

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

    [SlashCommand("명령어", "토리가 할 수 있는 일은~!")]
    public async Task ListCommandsAsync()
    {
        var slashCommands = _interactionService.SlashCommands;

        var description = string.Join("\n", slashCommands.Select(cmd => $"`/{cmd.Name}` : {cmd.Description}"));

        var embed = new EmbedBuilder()
            .WithTitle("✨ 토리의 명령어 목록")
            .WithDescription(string.IsNullOrEmpty(description) ? "등록된 명령어가 없습니다." : description)
            .WithColor(Color.Parse("FF0033")) // 테토 시그니처 컬러
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("토리", "토리가 대답합니다!")]
    public async Task HandleToriCommand()
    {
        try
        {
            await RespondAsync("여기, 여기! 토리 여기 있어!");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("토리", ex, "토리가 힘든 일을 열심히 하고 있습니다! \n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

    [SlashCommand("안녕", "토리에게 인사~")]
    public async Task HelloToriCommand()
    {
        try
        {
            await RespondAsync("안녕~! 잘 지냈어?");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("안녕", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

    [SlashCommand("출첵", "나 오늘도 왔지~")]
    public async Task CheckInAsync()
    {
        try
        {
            long userId = (long)Context.User.Id;
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            using var db = _dbService.GetConnection();
            db.Open();

            string selectQuery = "SELECT Points, LastCheckIn FROM Users WHERE UserId = @UserId";
            using var selectCmd = new MySqlCommand(selectQuery, db);
            selectCmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = selectCmd.ExecuteReader();

            if (reader.Read())
            {
                long points = reader.GetInt64(0);
                string lastCheckIn = reader.IsDBNull(1) ? "" : reader.GetDateTime(1).ToString("yyyy-MM-dd HH:mm:ss");

                if (lastCheckIn == today)
                {
                    await RespondAsync("어~? 오늘은 출석 체크 이미 했자나~!", ephemeral: true);
                    return;
                }

                reader.Close();
                string updateQuery = "UPDATE Users SET Points = Points + 100, LastCheckIn = @Today WHERE UserId = @UserId";
                using var updateCmd = new MySqlCommand(updateQuery, db);
                updateCmd.Parameters.AddWithValue("@Today", today);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.ExecuteNonQuery();

                await RespondAsync($"출석 체크 완료! 🎁 **100포인트**가져가~ (현재 포인트: {points + 100})");
            }
            else
            {
                reader.Close();
                string insertQuery = "INSERT INTO Users (UserId, Points, LastCheckIn) VALUES (@UserId, 100, @Today)";
                using var insertCmd = new MySqlCommand(insertQuery, db);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@Today", today);
                insertCmd.ExecuteNonQuery();

                await RespondAsync("오? 못 보던 얼굴인데...?\n아~ 처음이야~!\n알았어! **100포인트**를 선물로 줄게!");
            }

            _logger.LogInformation("{User}님이 출석체크를 완료했습니다.", Context.User.Username);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("출첵", ex, "토리의 메모장이 사라졌습니다!\n*DB 연동에 문제가 있을 수 있습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("포인트", "내 돈이 다 어디갔지...?")]
    public async Task CheckPointsAsync(IUser? targetUser = null)
    {
        try
        {
            var user = targetUser ?? Context.User;

            if (user.Id == Context.Client.CurrentUser.Id)
            {
                await RespondAsync($"나잖아! 나는 당연히.... 엄청 많다구~!");
                return;
            }

            long userId = (long)user.Id;

            using var db = _dbService.GetConnection();
            db.Open();

            string query = "SELECT Points FROM Users WHERE UserId = @UserId";
            using var cmd = new MySqlCommand(query, db);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var result = cmd.ExecuteScalar();

            if (result != null)
            {
                long points = Convert.ToInt64(result);
                
                if (user.Id == Context.User.Id)
                {
                    await RespondAsync($"잠시만 기다려봐 {user.Username}!\n\n아 찾았다! 너는 **{points}포인트**야!");
                }
                else
                {
                    await RespondAsync($"아~ {user.Username}! 잠시만... \n아! 여깄다! ||**{points}포인트**||네! 근데 이건 왜.....?");
                }
            }
            else
            {
                if (user.Id == Context.User.Id)
                {
                    await RespondAsync("어? 안녕! `/출첵`으로 출석체크 하고 와!", ephemeral: true);
                }
                else
                {
                    await RespondAsync($"{user.Username}? 그런 애는 잘 모르겠는데?", ephemeral: true);
                }
            }
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("포인트", ex, "토리의 메모장이 찢어져있습니다 ㅠㅠ\n*DB에서 데이터를 읽어오다가 실패했습니다.*");
        }
    }

    [SlashCommand("빨간미쿠", "하지 마!")]
    public async Task RedMikuToriCommand()
    {
        try
        {
            await RespondAsync($"빨간 미쿠 **아니거든~!?**");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("빨간미쿠", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

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

    [SlashCommand("타임라인", "지금까지 우리 서버는...")]
    public async Task ShowServerTimelineAsync()
    {
        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", "timeline.json");

            if (!File.Exists(filePath))
            {
                await RespondAsync("아직 서버 타임라인 기록(timeline.json)이 비어 있어!", ephemeral: true);
                return;
            }

            string jsonString = await File.ReadAllTextAsync(filePath);
            var timeline = JsonSerializer.Deserialize<List<TimelineItem>>(jsonString);

            if (timeline == null || timeline.Count == 0)
            {
                await RespondAsync("아직 역사가 없어! 새출발!");
                return;
            }

            var timelineListText = string.Join("\n\n", timeline.Select(item => $"📅 **{item.Date}** - {item.Title}\n> {item.Description}"));

            string message =
                $"📜 **[토리의 서버 역사관]** 우리 서버가 걸어온 길이야!\n\n" +
                $"{timelineListText}";

            await RespondAsync(message);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("타임라인", ex, "역사관 보물들이 서로 섞여버렸습니다!\n*타임라인을 저장하는 JSON 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("프로필", "넌 누구냐!")]
    public async Task ShowProfileAsync(IUser? targetUser = null)
    {
        try
        {
            var user = targetUser ?? Context.User;

            if (user.Id == Context.Client.CurrentUser.Id)
            {
                await RespondAsync("나잖아! 내 프로필은 영원한 비밀~", ephemeral: true);
                return;
            }

            long userId = (long)user.Id;

            using var db = _dbService.GetConnection();
            db.Open();

            string query = "SELECT Points, Titles, LastCheckIn FROM Users WHERE UserId = @UserId";
            using var cmd = new MySqlCommand(query, db);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                if (user.Id == Context.User.Id)
                {
                    await RespondAsync("어? 안녕! 아직 가입이 안 되어 있네. `/출첵`으로 출석체크를 먼저 해봐!", ephemeral: true);
                }
                else
                {
                    await RespondAsync($"{user.Username}? 그런 친구는 아직 데이터에 없는데...?", ephemeral: true);
                }
                return;
            }

            long points = reader.GetInt64(0);
            string titlesJson = reader.IsDBNull(1) ? "" : reader.GetString(1);
            string lastCheckIn = reader.IsDBNull(2) ? "" : reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss");
            reader.Close();

            string equippedTitleKey = string.IsNullOrEmpty(titlesJson)
                ? "Newbie"
                : (JsonSerializer.Deserialize<model.UserTitleData>(titlesJson)?.EquippedTitleKey ?? "Newbie");

            string equippedTitle = "[뉴비]";
            string titlesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", "titles.json");
            if (File.Exists(titlesPath))
            {
                var titleDefs = JsonSerializer.Deserialize<Dictionary<string, model.TitleInfo>>(await File.ReadAllTextAsync(titlesPath));
                if (titleDefs != null && titleDefs.TryGetValue(equippedTitleKey, out var titleInfo))
                {
                    equippedTitle = titleInfo.Name;
                }
            }

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string checkInStatus = lastCheckIn == today ? "오늘 완료!" : "아직 안 함 (`/출첵`을 눌러줘!)";

            var embed = new EmbedBuilder()
                .WithTitle($"🌟 {user.Username}님의 프로필 카드")
                .WithColor(Color.Blue)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .AddField("칭호", equippedTitle, true)
                .AddField("포인트", $"{points:N0} P", true)
                .AddField("출석 상태", checkInStatus, false)
                .WithFooter("토리의 서버 관리 시스템", Context.Client.CurrentUser.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            string flavorText = (user.Id == Context.User.Id) 
                ? "자, 여기 네 프로필이야!" 
                : $"어~ {user.Username}의 프로필을 뒤져봤지! 어떤가 봐봐~";

            await RespondAsync(flavorText, embed: embed);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("프로필", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 메모장에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

    [SlashCommand("상점", "골라골라 맘껏 골라~")]
    public async Task ShowShopAsync()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", "titles.json");
            if (!File.Exists(path))
            {
                await RespondAsync("아직 상점 상품 목록(titles.json)이 준비되지 않았어!", ephemeral: true);
                return;
            }

            string jsonString = await File.ReadAllTextAsync(path);
            var titles = JsonSerializer.Deserialize<Dictionary<string, model.TitleInfo>>(jsonString);

            if (titles == null || titles.Count == 0)
            {
                await RespondAsync("우리 재고 없어!", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("🛍️ [토리의 칭호 상점]")
                .WithDescription("모은 포인트로 멋진 칭호를 구매해봐!\n구매는 `/칭호구매 <칭호키>`로 할 수 있어.")
                .WithColor(Color.Gold);

            foreach (var pair in titles)
            {
                string key = pair.Key;
                var info = pair.Value;

                if (info.Price > 0)
                {
                    embed.AddField(
                        $"{info.Name} (코드: `{key}`)", 
                        $"설명: {info.Description}\n가격: **{info.Price:N0} P**", 
                        false
                    );
                }
            }

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("상점", ex, "메모장이 고장났습니다\n*칭호를 저장하는 JSON 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("칭호구매", "드디어... 손에 넣다니...!")]
    public async Task BuyTitleAsync([Summary(description: "상점에 적힌 칭호 코드 (예: MorningBird)")] string titleKey)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", "titles.json");
            if (!File.Exists(path))
            {
                await FollowupAsync("상점 데이터(titles.json)가 없어!", ephemeral: true);
                return;
            }

            string jsonString = await File.ReadAllTextAsync(path);
            var titles = JsonSerializer.Deserialize<Dictionary<string, model.TitleInfo>>(jsonString);

            if (titles == null || !titles.TryGetValue(titleKey, out var targetTitle))
            {
                await FollowupAsync("그런 코드를 가진 칭호는 상점에 없는데?", ephemeral: true);
                return;
            }

            if (targetTitle.Price <= 0)
            {
                await FollowupAsync("이 칭호는 돈 주고 살 수 있는 게 아니야!", ephemeral: true);
                return;
            }

            long userId = (long)Context.User.Id;
            long points;
            string titlesJson;

            using (var db = _dbService.GetConnection())
            {
                db.Open();
                const string selectQuery = "SELECT Points, Titles FROM Users WHERE UserId = @UserId";
                using var selectCmd = new MySqlCommand(selectQuery, db);
                selectCmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = selectCmd.ExecuteReader();
                if (!reader.Read())
                {
                    await FollowupAsync("아직 가입(출석체크)을 안 해서 포인트가 없어! `/출첵`부터 해봐!", ephemeral: true);
                    return;
                }

                points = reader.GetInt64(0);
                titlesJson = reader.IsDBNull(1) ? "" : reader.GetString(1);
            }

            var userTitles = string.IsNullOrEmpty(titlesJson)
                ? new model.UserTitleData()
                : JsonSerializer.Deserialize<model.UserTitleData>(titlesJson) ?? new model.UserTitleData();

            if (userTitles.OwnedTitleKeys.Contains(titleKey))
            {
                await FollowupAsync("너 그거 이미 가지고 있잖아!", ephemeral: true);
                return;
            }

            if (points < targetTitle.Price)
            {
                await FollowupAsync($"포인트가 부족해! (현재: {points:N0} P / 필요: {targetTitle.Price:N0} P)", ephemeral: true);
                return;
            }

            long remainingPoints = points - targetTitle.Price;
            userTitles.OwnedTitleKeys.Add(titleKey);
            string updatedTitlesJson = JsonSerializer.Serialize(userTitles);

            using (var db = _dbService.GetConnection())
            {
                db.Open();
                const string updateQuery = "UPDATE Users SET Points = @Points, Titles = @Titles WHERE UserId = @UserId";
                using var updateCmd = new MySqlCommand(updateQuery, db);
                updateCmd.Parameters.AddWithValue("@Points", remainingPoints);
                updateCmd.Parameters.AddWithValue("@Titles", updatedTitlesJson);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.ExecuteNonQuery();
            }

            await FollowupAsync($"🎉 축하해! **{targetTitle.Name}** 칭호를 구매했어!\n잔여 포인트: **{remainingPoints:N0} P** (장착은 `/칭호장착 {titleKey}`로 해봐!)");

            _logger.LogInformation("{User}님이 '{TitleKey}' 칭호를 구매했습니다. 잔여 포인트={RemainingPoints}", Context.User.Username, titleKey, remainingPoints);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("칭호구매", ex, "상점이 어쩔 수 없이 잠시 쉬는 중입니다!\n*DB SQL 또는 JSON 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("칭호장착", "반짝 반짝!")]
    public async Task EquipTitleAsync([Summary(description: "장착할 칭호 코드 (예: MorningBird)")] string titleKey)
    {
        try
        {
            long userId = (long)Context.User.Id;

            using var db = _dbService.GetConnection();
            db.Open();

            string selectQuery = "SELECT Titles FROM Users WHERE UserId = @UserId";
            using var selectCmd = new MySqlCommand(selectQuery, db);
            selectCmd.Parameters.AddWithValue("@UserId", userId);

            var result = selectCmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                await RespondAsync("칭호가 없잖아! `/상점`에 가봐!", ephemeral: true);
                return;
            }

            string titlesJson = result.ToString()!;
            var userTitles = JsonSerializer.Deserialize<model.UserTitleData>(titlesJson) ?? new model.UserTitleData();

            if (!userTitles.OwnedTitleKeys.Contains(titleKey))
            {
                await RespondAsync("너 그 칭호 안 가지고 있는데... 도둑질은 나쁜 거야!", ephemeral: true);
                return;
            }

            userTitles.EquippedTitleKey = titleKey;
            string updatedTitlesJson = JsonSerializer.Serialize(userTitles);

            string updateQuery = "UPDATE Users SET Titles = @Titles WHERE UserId = @UserId";
            using var updateCmd = new MySqlCommand(updateQuery, db);
            updateCmd.Parameters.AddWithValue("@Titles", updatedTitlesJson);
            updateCmd.Parameters.AddWithValue("@UserId", userId);
            updateCmd.ExecuteNonQuery();

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", "titles.json");
            string titleName = titleKey;
            if (File.Exists(path))
            {
                var titles = JsonSerializer.Deserialize<Dictionary<string, model.TitleInfo>>(await File.ReadAllTextAsync(path));
                if (titles != null && titles.TryGetValue(titleKey, out var info))
                {
                    titleName = info.Name;
                }
            }

            await RespondAsync($"✨ 칭호 장착 완료! 이제부터 네 프로필에 **{titleName}**(이)가 반짝일 거야!");

            _logger.LogInformation("{User}님이 '{TitleKey}' 칭호를 장착했습니다.", Context.User.Username, titleKey);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("칭호장착", ex, "칭호에 적응하지 못 했습니다!\n*DB SQL에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("도박", "불법 스포츠 토토 신고는 1899-1119")]
    public async Task GambleAsync(
        [Summary(description: "도박 종류를 선택해줘")] 
        [Choice("주사위 홀짝", "오드이븐")] 
        [Choice("확률 룰렛 (하이리스크)", "룰렛")] 
        string gameType,
        
        [Summary(description: "배팅할 포인트")] 
        long betPoints,
        
        [Summary(description: "홀짝일 경우 '홀' 또는 '짝' 입력")] 
        string arg = "")
    {
        try
        {
            if (betPoints <= 0)
            {
                await RespondAsync("배팅 안 할거야?!", ephemeral: true);
                return;
            }

            long userId = (long)Context.User.Id;
            long currentPoints = 0;

            using (var db = _dbService.GetConnection())
            {
                db.Open();
                string selectQuery = "SELECT Points FROM Users WHERE UserId = @UserId";
                using var selectCmd = new MySqlCommand(selectQuery, db);
                selectCmd.Parameters.AddWithValue("@UserId", userId);

                var result = selectCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    await RespondAsync("아직 출석체크를 안 해서 포인트가 없어! `/출첵`부터 해봐.", ephemeral: true);
                    return;
                }

                currentPoints = Convert.ToInt64(result);

                if (currentPoints < betPoints)
                {
                    await RespondAsync($"포인트가 부족해! (현재: {currentPoints:N0} P)", ephemeral: true);
                    return;
                }
            }

            long finalPoints = currentPoints;
            EmbedBuilder embed = new EmbedBuilder();
            bool gameWasWin = false;

            if (gameType == "오드이븐")
            {
                if (arg != "홀" && arg != "짝")
                {
                    await RespondAsync("홀짝 게임은 세 번째 칸에 **'홀'** 또는 **'짝'**을 적어야 해!", ephemeral: true);
                    return;
                }

                int diceResult = Random.Shared.Next(1, 7);
                string actualResult = (diceResult % 2 != 0) ? "홀" : "짝";
                bool isWin = (arg == actualResult);
                gameWasWin = isWin;

                if (isWin)
                {
                    long winnings = betPoints * 2;
                    finalPoints = currentPoints - betPoints + winnings;
                }
                else
                {
                    finalPoints = currentPoints - betPoints;
                }

                using (var db = _dbService.GetConnection())
                {
                    db.Open();
                    string updateQuery = "UPDATE Users SET Points = @Points WHERE UserId = @UserId";
                    using var updateCmd = new MySqlCommand(updateQuery, db);
                    updateCmd.Parameters.AddWithValue("@Points", finalPoints);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    updateCmd.ExecuteNonQuery();
                }

                embed.WithTitle("🎲 [토리의 주사위 홀짝 대결]")
                     .WithDescription(isWin ? "🎉 **정답! 대박이야!**" : "💸 **아쉽네... 꽝이야!**")
                     .AddField("주사위 눈", $"**{diceResult}** ({actualResult})", true)
                     .AddField("내 선택", arg, true)
                     .AddField("결과", isWin ? $"+{betPoints * 2:N0} P 획득!" : $"-{betPoints:N0} P 증발...", false)
                     .WithFooter($"남은 잔액: {finalPoints:N0} P")
                     .WithColor(isWin ? Color.Green : Color.Red);
            }
            else if (gameType == "룰렛")
            {
                int roll = Random.Shared.Next(1, 1001);
                double multiplier = 0;
                bool isWin = false;

                if (roll >= 1 && roll <= 10)
                {
                    multiplier = 5.0;
                    isWin = true;
                }
                else if (roll >= 11 && roll <= 100)
                {
                    multiplier = 2.0;
                    isWin = true;
                }
                else if (roll >= 101 && roll <= 500)
                {
                    multiplier = 1.25;
                    isWin = true;
                }
                else
                {
                    isWin = false;
                }

                gameWasWin = isWin;

                if (isWin)
                {
                    long winnings = (long)(betPoints * multiplier);
                    finalPoints = currentPoints - betPoints + winnings;
                }
                else
                {
                    finalPoints = currentPoints - betPoints;
                }

                using (var db = _dbService.GetConnection())
                {
                    db.Open();
                    string updateQuery = "UPDATE Users SET Points = @Points WHERE UserId = @UserId";
                    using var updateCmd = new MySqlCommand(updateQuery, db);
                    updateCmd.Parameters.AddWithValue("@Points", finalPoints);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    updateCmd.ExecuteNonQuery();
                }

                string gradeText = "꽝 (50%)";
                if (multiplier == 5.0) gradeText = "🌟 [1% 전설의 대박] x5배!";
                else if (multiplier == 2.0) gradeText = "💰 [9% 대성공] x2배!";
                else if (multiplier == 1.25) gradeText = "✨ [40% 소소한 이득] x1.25배!";

                embed.WithTitle("🎯 [토리의 1000선택 룰렛]")
                     .WithDescription(isWin ? "✨ **당첨을 축하해!**" : "💥 **아쉽게 꽝이야...**")
                     .AddField("추첨 번호 (1~1000)", $"**{roll}**", true)
                     .AddField("당첨 등급", gradeText, true)
                     .AddField("결과", isWin ? $"+{(long)(betPoints * multiplier):N0} P 획득!" : $"-{betPoints:N0} P 차감...", false)
                     .WithFooter($"남은 잔액: {finalPoints:N0} P")
                     .WithColor(isWin ? Color.Gold : Color.DarkRed);
            }

            await RespondAsync(embed: embed.Build());

            _logger.LogInformation(
                "{User}님이 '{GameType}' 도박을 했습니다. 배팅={BetPoints}, 결과={Result}, 최종잔액={FinalPoints}",
                Context.User.Username, gameType, betPoints, gameWasWin ? "승리" : "패배", finalPoints);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("도박", ex, "계산이 너무 어려워서 실수 했습니다!\n*DB SQL 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("도박확률", "도박 나빠!")]
    public async Task GamblePercentAsync(
        [Summary(description: "도박 종류를 선택해줘")] 
        [Choice("주사위 홀짝", "오드이븐")] 
        [Choice("확률 룰렛 (하이리스크)", "룰렛")] 
        string gameType)
    {
        try
        {
            var embed = new EmbedBuilder();

            if (gameType == "오드이븐")
            {
                embed.WithTitle("🎲 [주사위 홀짝 확률 안내]")
                     .WithDescription("주사위(1~6)를 굴려 '홀' 또는 '짝'을 맞히는 단순 명쾌한 게임입니다!")
                     .WithColor(Color.Blue)
                     .AddField("🎲 승리 확률", "`50%` (주사위 눈 1, 3, 5 = 홀 / 2, 4, 6 = 짝)", false)
                     .AddField("💰 배율 보상", "승리 시 배팅금의 **2배** 획득", false)
                     .WithFooter("단순하지만 방심은 금물!", Context.Client.CurrentUser.GetAvatarUrl())
                     .WithCurrentTimestamp();
            }
            else if (gameType == "룰렛")
            {
                embed.WithTitle("🎯 [1000선택 룰렛 확률 안내]")
                     .WithDescription("1부터 1000까지의 무작위 번호를 뽑아 등급별 당첨을 겨루는 하이리스크 게임입니다!")
                     .WithColor(Color.Purple)
                     .AddField("🌟 전설의 대박 (1%)", "• **당첨 번호**: `1 ~ 10`\n• **보상 배율**: 배팅금의 **5배**", false)
                     .AddField("💰 대성공 (9%)", "• **당첨 번호**: `11 ~ 100`\n• **보상 배율**: 배팅금의 **2배**", false)
                     .AddField("✨ 소소한 당첨 (40%)", "• **당첨 번호**: `101 ~ 500`\n• **보상 배율**: 배팅금의 **1.25배**", false)
                     .AddField("💥 꽝 (50%)", "• **당첨 번호**: `501 ~ 1000`\n• **보상 배율**: **0배**", false)
                     .WithFooter("과도한 도박은 포인트 탕진의 지름길!", Context.Client.CurrentUser.GetAvatarUrl())
                     .WithCurrentTimestamp();
            }

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("도박확률", ex, "도박 확률 정보를 읽어오는 데 실패했습니다!");
        }
    }

    [SlashCommand("보카로퀴즈", "보카로 곡을 보고 주관식으로 제목을 맞혀봐! (정답 시 포인트 획득)")]
    public async Task VocaloidQuizAsync()
    {
        System.Func<SocketMessage, Task>? handler = null;
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", "songs.json");
            if (!File.Exists(path))
            {
                await RespondAsync("퀴즈 데이터(songs.json) 파일이 없어! 만들어줘.", ephemeral: true);
                return;
            }

            string jsonString = await File.ReadAllTextAsync(path);
            var quizList = JsonSerializer.Deserialize<List<SongQuizModel>>(jsonString);

            if (quizList == null || quizList.Count == 0)
            {
                await RespondAsync("퀴즈 목록이 비어 있어!", ephemeral: true);
                return;
            }

            var randomQuiz = quizList[Random.Shared.Next(quizList.Count)];

            var embed = new EmbedBuilder()
                .WithTitle("🎵 [보카로 주관식 곡 맞히기 퀴즈!]")
                .WithDescription("제한 시간 **15초** 안에 이 곡의 **제목**을 이 채널에 채팅으로 입력해!")
                .AddField($"힌트 ({randomQuiz.HintType})", $"**{randomQuiz.Hint}**", false)
                .AddField("부가 설명", randomQuiz.Description, false)
                .WithFooter("가장 먼저 정확한 제목을 치는 사람이 승리합니다!")
                .WithColor(Color.Magenta)
                .Build();

            await RespondAsync(embed: embed);

            var tcs = new TaskCompletionSource<SocketMessage>();

            handler = msg =>
            {
                if (msg.Channel.Id == Context.Channel.Id && !msg.Author.IsBot)
                {
                    if (msg.Content.Trim() == randomQuiz.Title)
                    {
                        tcs.TrySetResult(msg);
                    }
                }
                return Task.CompletedTask;
            };

            Context.Client.MessageReceived += handler;

            var task = tcs.Task;
            if (await Task.WhenAny(task, Task.Delay(15000)) == task)
            {
                var winningMsg = await task;
                long winnerId = (long)winningMsg.Author.Id;
                long rewardPoints = 500;

                using (var db = _dbService.GetConnection())
                {
                    db.Open();
                    // MySQL 전용 Upsert 구문 사용
                    string query = @"
                        INSERT INTO Users (UserId, Points, LastCheckIn) 
                        VALUES (@UserId, @Reward, 'None')
                        ON DUPLICATE KEY UPDATE Points = Points + @Reward;";
                    using var cmd = new MySqlCommand(query, db);
                    cmd.Parameters.AddWithValue("@Reward", rewardPoints);
                    cmd.Parameters.AddWithValue("@UserId", winnerId);
                    cmd.ExecuteNonQuery();
                }

                await Context.Channel.SendMessageAsync($"🎉 정답이야! **{winningMsg.Author.Username}**님이 가장 먼저 맞혔어!\n🎁 보상으로 **+500 P** 획득! (정답: **{randomQuiz.Title}**) 데이터베이스에 적립 완료!");
                _logger.LogInformation("보카로퀴즈 정답자: {Winner}, 문제: {Title}, 보상: {Reward}P", winningMsg.Author.Username, randomQuiz.Title, rewardPoints);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"⏰ 시간 초과! 아쉽게도 아무도 정답을 못 맞혔네... (정답은 **'{randomQuiz.Title}'**이었어!)");
                _logger.LogInformation("보카로퀴즈 시간 초과. 문제: {Title}", randomQuiz.Title);
            }
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("보카로퀴즈", ex, "문제 시스템에 오류를 찾아서 고치는 중입니다.\n*DB SQL 또는 문제를 저장하는 JSON 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
        finally
        {
            if (handler != null)
            {
                Context.Client.MessageReceived -= handler;
            }
        }
    }

    [SlashCommand("가챠", "500포인트를 미끼로 보컬로이드 스카우트!\n(워.....월척이닷~!)")]
    public async Task GachaAsync()
    {
        if (Context.Interaction.HasResponded) return;
        try
        {
            long userId = (long)Context.User.Id;
            long baitPrice = 500; // 미끼 가격 500P로 인상

            using (var db = _dbService.GetConnection())
            {
                db.Open();

                // 1. 포인트 잔액 조회 및 검사
                string checkQuery = "SELECT Points FROM Users WHERE UserId = @UserId";
                using var checkCmd = new MySqlCommand(checkQuery, db);
                checkCmd.Parameters.AddWithValue("@UserId", userId);

                var existingPointsObj = checkCmd.ExecuteScalar();

                if (existingPointsObj == null || existingPointsObj == DBNull.Value)
                {
                    await RespondAsync("아직 출석체크를 안 해서 포인트가 없어! `/출첵`부터 해봐.", ephemeral: true);
                    return;
                }

                long currentPoints = Convert.ToInt64(existingPointsObj);
                if (currentPoints < baitPrice)
                {
                    await RespondAsync($"포인트가 부족해! 낚시를 하려면 최소 **{baitPrice:N0} P**가 필요해! (현재: {currentPoints:N0} P)", ephemeral: true);
                    return;
                }

                // 2. 미끼값 500P 차감
                string deductQuery = "UPDATE Users SET Points = Points - @BaitPrice WHERE UserId = @UserId";
                using var deductCmd = new MySqlCommand(deductQuery, db);
                deductCmd.Parameters.AddWithValue("@BaitPrice", baitPrice);
                deductCmd.Parameters.AddWithValue("@UserId", userId);
                deductCmd.ExecuteNonQuery();
            }

            // 3. 확률 뽑기 (80% / 18% / 2%)
            int roll = Random.Shared.Next(1, 101); // 1~100
            var targetPool = FishingCharacterPool;

            if (roll <= 80)        // 1~80 (80% 확률)
                targetPool = FishingCharacterPool.FindAll(c => c.Grade == "일반");
            else if (roll <= 98)   // 81~98 (18% 확률)
                targetPool = FishingCharacterPool.FindAll(c => c.Grade == "희귀");
            else                   // 99~100 (2% 확률)
                targetPool = FishingCharacterPool.FindAll(c => c.Grade == "전설");

            var caughtChar = targetPool[Random.Shared.Next(targetPool.Count)];

            bool isNew = false;
            long totalCatchCount = 1;

            // 4. 잡은 캐릭터 도감 및 지원금 지급 처리
            using (var db = _dbService.GetConnection())
            {
                db.Open();

                string checkFishQuery = "SELECT CatchCount FROM UserFishes WHERE UserId = @UserId AND FishName = @FishName";
                using var checkFishCmd = new MySqlCommand(checkFishQuery, db);
                checkFishCmd.Parameters.AddWithValue("@UserId", userId);
                checkFishCmd.Parameters.AddWithValue("@FishName", caughtChar.Name);

                var existingCount = checkFishCmd.ExecuteScalar();

                if (existingCount != null && existingCount != DBNull.Value)
                {
                    totalCatchCount = Convert.ToInt64(existingCount) + 1;
                    string updateQuery = "UPDATE UserFishes SET CatchCount = @Count WHERE UserId = @UserId AND FishName = @FishName";
                    using var updateCmd = new MySqlCommand(updateQuery, db);
                    updateCmd.Parameters.AddWithValue("@Count", totalCatchCount);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    updateCmd.Parameters.AddWithValue("@FishName", caughtChar.Name);
                    updateCmd.ExecuteNonQuery();
                }
                else
                {
                    isNew = true;
                    string insertQuery = "INSERT INTO UserFishes (UserId, FishName, Grade, Length, CatchCount) VALUES (@UserId, @FishName, @Grade, 0, 1)";
                    using var insertCmd = new MySqlCommand(insertQuery, db);
                    insertCmd.Parameters.AddWithValue("@UserId", userId);
                    insertCmd.Parameters.AddWithValue("@FishName", caughtChar.Name);
                    insertCmd.Parameters.AddWithValue("@Grade", caughtChar.Grade);
                    insertCmd.ExecuteNonQuery();
                }

                // 캐릭터 환급 지원금 추가
                string refundQuery = "UPDATE Users SET Points = Points + @Price WHERE UserId = @UserId";
                using var refundCmd = new MySqlCommand(refundQuery, db);
                refundCmd.Parameters.AddWithValue("@Price", caughtChar.Price);
                refundCmd.Parameters.AddWithValue("@UserId", userId);
                refundCmd.ExecuteNonQuery();
            }

            Color embedColor = Color.Blue;
            if (caughtChar.Grade == "희귀") embedColor = Color.Purple;
            if (caughtChar.Grade == "전설") embedColor = Color.Gold;

            var embed = new EmbedBuilder()
                .WithTitle("🎧 [보카로 캐릭터 스카우트 성공!]")
                .WithDescription(isNew ? "✨ **[NEW 도감 등록!]** 새로운 캐릭터를 만났어요!" : $"🎶 또 만났네요! (총 {totalCatchCount}번째 영입)")
                .AddField("캐릭터", $"**{caughtChar.Name}** ({caughtChar.Grade})", true)
                .AddField("지원금 환급", $"+{caughtChar.Price:N0} P (손실: -{baitPrice - caughtChar.Price:N0} P)", true)
                .WithColor(embedColor)
                .Build();

            await RespondAsync(embed: embed);

            _logger.LogInformation(
                "{User}님이 가챠로 '{Character}' ({Grade})을(를) 획득했습니다. 신규={IsNew}, 누적={TotalCatchCount}",
                Context.User.Username, caughtChar.Name, caughtChar.Grade, isNew, totalCatchCount);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("가챠", ex, "*DB SQL에 실패했습니다.*\n*점검:*\n- 관리자 DM");
        }
    }

    [SlashCommand("가챠확률", "가챠를 뽑을 때의 확률은...?")]
    public async Task GachaPercentAsync()
    {
        try
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎰 [토리의 캐릭터 가챠 확률 안내]")
                .WithDescription("500포인트를 소모해 보컬로이드 캐릭터를 영입해보세요!\n각 등급별 등장 확률 및 환급 지원금 안내입니다.")
                .WithColor(Color.Gold) // 골드 색상으로 강조
                .AddField("⚪ 일반 등급 (80%)", "• **등장 확률**: `80%`\n• **환급 지원금**: `50 ~ 150 P`\n• **등장 캐릭터**: 즌다몬, 카이토, 유키, 카후, 우이, 유카, 린, 렌, 네루, 레이", false)
                .AddField("🟣 희귀 등급 (18%)", "• **등장 확률**: `18%`\n• **환급 지원금**: `300 ~ 500 P`\n• **등장 캐릭터**: 테토(한국어), 미쿠(한국어), 테토(영어), 미쿠(영어)", false)
                .AddField("🟡 전설 등급 (2%)", "• **등장 확률**: `2%` (대박! 🎉)\n• **환급 지원금**: `1,000 ~ 2,000 P`\n• **등장 캐릭터**: 테토(일본어), 미쿠(일본어), 작곡(전설의 마스터)", false)
                .WithFooter("소모 포인트: 회당 500 P", Context.Client.CurrentUser.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("가챠확률", ex, "확률표를 가져오는 중에 메모리를 쏟았습니다!");
        }
    }

    [SlashCommand("친해지기", "나...나랑...?!")]
    public async Task FriendAsync(
        [Summary(description: "행동을 선택해줘")] 
        [Choice("인사하기", "인사")] 
        [Choice("쓰담쓰담~", "쓰다듬기")] 
        [Choice("바라보기", "바라보기")]
        string Action)
    {
        try
        {
            if(ActionofMove.TryGetValue(Action, out string? a)) await RespondAsync(a);
            else await RespondAsync("앗, 그 기능은 아직 준비 중이야! 조금만 기다려줘~", ephemeral: true);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("친해지기", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 디스코드 서버에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM");
        }
    }

    [SlashCommand("초대링크", "현재 채널의 서버 초대 링크를 생성합니다.")]
    public async Task CreateInviteLinkAsync(
        [Summary(description: "링크 만료 시간 (분 단위, 0 = 무제한)")] int maxAgeMinutes = 1440,
        [Summary(description: "최대 사용 횟수 (0 = 무제한)")] int maxUses = 0)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            if (Context.Channel is not ITextChannel channel)
            {
                await FollowupAsync("텍스트 채널에서만 초대 링크를 만들 수 있어!", ephemeral: true);
                return;
            }

            int? maxAgeSeconds = maxAgeMinutes > 0 ? maxAgeMinutes * 60 : null;
            int? uses = maxUses > 0 ? maxUses : null;

            var invite = await channel.CreateInviteAsync(
                maxAge: maxAgeSeconds,
                maxUses: uses,
                isTemporary: false,
                isUnique: true
            );

            await FollowupAsync($"짜잔! 초대 링크가 생성되었어!\n친구 많이 많이 데리고 오라구~!\n🔗 {invite.Url}", ephemeral: true);

            _logger.LogInformation("{User}님이 #{Channel} 채널의 초대 링크를 생성했습니다: {Url}", 
                Context.User.Username, channel.Name, invite.Url);
        }
        catch (Discord.Net.HttpException httpEx) when (httpEx.HttpCode == System.Net.HttpStatusCode.Forbidden)
        {
            await FollowupAsync("토리가 이 채널에서 **'초대 코드 만들기'** 권한이 없어!", ephemeral: true);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("초대링크", ex, "초대 링크를 생성하는 도중 오류가 발생했어!");
        }
    }

    [SlashCommand("웹사이트", "토리의 웹사이트")]
    public async Task ShowWebSiteUrlAsync()
    {
        try
        {
            await RespondAsync("내 웹사이트는 여기있어! https://tori-9gxd.onrender.com");
        } catch (Exception ex)
        {
            await RespondErrorAsync("웹사이트", ex, "웹사이트 링크를 보내는 중에 오류가 났습니다!");
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
            await RespondErrorAsync("비밀키", ex, "키를 보여주는 중에 오류가 났습니다!");
        }
    }

    [SlashCommand("버전", "테토는 얼마나 성장했어?")]
    public async Task ShowVersionAsync()
    {
        try
        {
            string[] Ver = BotEnv.botVersion.Split(".");
            await RespondAsync($"나는 {Ver[1]}번 학습하고 {Ver[2]}번 고쳐졌어!");
        } catch (Exception ex)
        {
            await RespondErrorAsync("버전", ex, "버전을 보여주는 중 오류가 났습니다!");
        }
    }
}