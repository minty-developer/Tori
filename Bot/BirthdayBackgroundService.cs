using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using model;

// 매일 한국시간(KST) 오전 9시에 그날이 생일인 캐릭터가 있는지 확인해서
// 지정된 채널에 축하 메시지를 보내는 백그라운드 서비스.
public class BirthdayBackgroundService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BirthdayBackgroundService> _logger;
    private const string BirthdayDataPath = "Birthday.json";

    // 알림을 띄울 디스코드 채널 ID (여기에 공지를 보낼 채널 ID 숫자를 넣으세요!)
    private const ulong TargetChannelId = 1479322191148613686;

    public BirthdayBackgroundService(DiscordSocketClient client, ILogger<BirthdayBackgroundService> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 봇이 완전히 로그인하고 준비될 때까지 살짝 대기
        while (_client.ConnectionState != Discord.ConnectionState.Connected)
        {
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("생일 알림 백그라운드 서비스가 시작되었습니다!");

        while (!stoppingToken.IsCancellationRequested)
        {
            // 현재 한국 시간(KST) 기준 계산
            TimeZoneInfo kstZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            DateTime nowKst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kstZone);

            // 예시: 매일 아침 9시 0분에 공지 날리고 싶을 때!
            if (nowKst.Hour == 9 && nowKst.Minute == 0)
            {
                await CheckAndSendBirthdayAsync(nowKst);

                // 1분 동안은 중복 실행 안 되도록 넉넉하게 대기
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            // 30초마다 시간이 되었는지 체크
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task CheckAndSendBirthdayAsync(DateTime targetDate)
    {
        try
        {
            if (!File.Exists(BirthdayDataPath))
            {
                _logger.LogWarning("생일 데이터 파일을 찾을 수 없습니다: {Path}", BirthdayDataPath);
                return;
            }

            string jsonString = await File.ReadAllTextAsync(BirthdayDataPath);
            var birthdays = JsonSerializer.Deserialize<List<BirthdayItem>>(jsonString);
            if (birthdays == null) return;

            var todayStars = birthdays.Where(b => b.Month == targetDate.Month && b.Day == targetDate.Day).ToList();

            if (todayStars.Count == 0)
            {
                _logger.LogInformation("오늘({Date})은 생일인 캐릭터가 없습니다.", targetDate.ToString("MM-dd"));
                return;
            }

            var channel = _client.GetChannel(TargetChannelId) as SocketTextChannel;
            if (channel == null)
            {
                _logger.LogWarning("생일 알림을 보낼 채널({ChannelId})을 찾을 수 없습니다.", TargetChannelId);
                return;
            }

            var starListText = string.Join("\n", todayStars.Select(star => $"🎉 **{star.Name}** 탄생일!"));

            string message =
                $"📢 **[토리의 생일 알림]** 오늘이 무슨 날인지 아는거야?\n\n" +
                $"{starListText}\n\n" +
                $"다들 축하의 한마디~";

            await channel.SendMessageAsync(message);

            _logger.LogInformation("생일 알림 전송 완료. 대상: {Names}", string.Join(", ", todayStars.Select(s => s.Name)));
        }
        catch (Exception ex)
        {
            // 💡 발생 상황: Birthday.json 형식이 깨졌거나, 지정된 채널에 메시지 전송 권한이 없을 때
            _logger.LogError(ex, "생일 알림 처리 중 오류가 발생했습니다.");
        }
    }
}
