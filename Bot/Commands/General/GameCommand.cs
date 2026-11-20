using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MySqlConnector;

public class GameCommands(DatabaseService dbService, ILogger<GameCommands> logger, Util util) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _dbService = dbService;
    private readonly ILogger<GameCommands> _logger = logger;
    private readonly Util _util = util;

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
            await _util.RespondErrorAsync("도박", ex, "계산이 너무 어려워서 실수 했습니다!\n*DB SQL 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
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
            await _util.RespondErrorAsync("도박확률", ex, "도박 확률 정보를 읽어오는 데 실패했습니다!");
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
            await _util.RespondErrorAsync("보카로퀴즈", ex, "문제 시스템에 오류를 찾아서 고치는 중입니다.\n*DB SQL 또는 문제를 저장하는 JSON 파싱에 실패했습니다.*\n*점검:*\n- 관리자 DM");
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
            await _util.RespondErrorAsync("가챠", ex, "*DB SQL에 실패했습니다.*\n*점검:*\n- 관리자 DM");
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
            await _util.RespondErrorAsync("가챠확률", ex, "확률표를 가져오는 중에 메모리를 쏟았습니다!");
        }
    }
}