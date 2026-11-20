using System.Text.Json;
using Discord;
using Discord.Interactions;
using MySqlConnector;

public class HelpCommands(DatabaseService dbService, ILogger<HelpCommands> logger, InteractionService interactionService, Util util) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _dbService = dbService;
    private readonly ILogger<HelpCommands> _logger = logger;
    private readonly InteractionService _interactionService = interactionService;
    private readonly Util _util = util;

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
            await _util.RespondErrorAsync("출첵", ex, "토리의 메모장이 사라졌습니다!\n*DB 연동에 문제가 있을 수 있습니다.*\n*점검:*\n- 관리자 DM", _logger);
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
            await _util.RespondErrorAsync("포인트", ex, "토리의 메모장이 찢어져있습니다 ㅠㅠ\n*DB에서 데이터를 읽어오다가 실패했습니다.*", _logger);
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
            await _util.RespondErrorAsync("프로필", ex, "토리가 아직 말을 못 들었습니다!\n*인터넷 연결이 끊겼거나 메모장에 문제가 있습니다!*\n*점검:*\n- 인터넷 연결\n- 관리자 DM", _logger);
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
            await _util.RespondErrorAsync("초대링크", ex, "초대 링크를 생성하는 도중 오류가 발생했어!", _logger);
        }
    }
}