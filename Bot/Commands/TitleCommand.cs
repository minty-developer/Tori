using System.Text.Json;
using Discord;
using Discord.Interactions;
using MySqlConnector;

public class TitleCommands(DatabaseService dbService, ILogger<TitleCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _dbService = dbService;
    private readonly ILogger<TitleCommands> _logger = logger;

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