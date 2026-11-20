using Discord;
using Discord.Interactions;

public class UserCommands(
    ILogger<UserCommands> logger,
    Util util,
    UserRepository userRepository
) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<UserCommands> _logger = logger;
    private readonly Util _util = util;
    private readonly UserRepository _repo = userRepository;

    [SlashCommand("출첵", "나 오늘도 왔지~")]
    public async Task CheckInAsync()
    {
        try
        {
            ulong userId = Context.User.Id;

            // 한국 시간 기준 날짜
            TimeZoneInfo kst = TimeZoneInfo.FindSystemTimeZoneById(
                "Korea Standard Time");

            DateOnly today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, kst));

            AttendanceResult result =
                await _repo.CheckInAsync(userId, today);

            if (result.IsAlreadyCheckedIn)
            {
                await RespondAsync(
                    "어~? 오늘은 출석 체크 이미 했자나~!",
                    ephemeral: true);

                return;
            }

            await RespondAsync(
                $"출석 체크 완료! 🎁 **100포인트** 가져가~ " +
                $"(현재 포인트: {result.Points})");

            _logger.LogInformation(
                "{User}님이 출석 체크를 완료했습니다.",
                Context.User.Username);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "출첵",
                ex,
                "토리의 메모장이 사라졌습니다!\n" +
                "*DB 연동에 문제가 있을 수 있습니다.*\n" +
                "*점검:*\n- 관리자 DM",
                _logger);
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
                await RespondAsync(
                    "나잖아! 내 프로필은 영원한 비밀~",
                    ephemeral: true);
                return;
            }

            var profile = await _repo.GetProfileAsync(user.Id);

            if (profile is null)
            {
                if (user.Id == Context.User.Id)
                {
                    await RespondAsync(
                        "어? 안녕! 아직 가입이 안 되어 있네. " +
                        "`/출첵`으로 출석체크를 먼저 해봐!",
                        ephemeral: true);
                }
                else
                {
                    await RespondAsync(
                        $"{user.Username}? 그런 친구는 아직 데이터에 없는데...?",
                        ephemeral: true);
                }

                return;
            }

            // 한국 시간 기준 날짜
            TimeZoneInfo kst = TimeZoneInfo.FindSystemTimeZoneById(
                "Korea Standard Time");

            DateOnly today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kst));

            string checkInStatus =
                profile.LastCheckIn == today
                    ? "오늘 완료!"
                    : "아직 안 함 (`/출첵`을 눌러줘!)";

            var embed = new EmbedBuilder()
                .WithTitle($"🌟 {user.Username}님의 프로필 카드")
                .WithColor(Color.Blue)
                .WithThumbnailUrl(
                    user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .AddField("칭호", profile.EquippedTitle, true)
                .AddField("포인트", $"{profile.Points:N0} P", true)
                .AddField("출석 상태", checkInStatus, false)
                .WithFooter(
                    "토리의 서버 관리 시스템",
                    Context.Client.CurrentUser.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            string flavorText = user.Id == Context.User.Id
                ? "자, 여기 네 프로필이야!"
                : $"어~ {user.Username}의 프로필을 뒤져봤지! 어떤가 봐봐~";

            await RespondAsync(flavorText, embed: embed);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync(
                "프로필",
                ex,
                "토리가 아직 말을 못 들었습니다!\n" +
                "*인터넷 연결이 끊겼거나 메모장에 문제가 있습니다!*\n" +
                "*점검:*\n- 인터넷 연결\n- 관리자 DM",
                _logger);
        }
    }
}