using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json;

// 일본어(히라가나/가타카나)가 포함된 메시지를 감지해서 자동으로 한국어 번역을 답글로 달아주는 서비스.
//
// 🔧 정리: DiscordBotService.cs에도 완전히 동일한 번역 로직(IsJapanese, TranslateToKoreanAsync,
//    MessageReceived 구독)이 중복으로 들어 있었다. 두 곳이 동시에 살아있으면 일본어 메시지 하나당
//    번역 답글이 두 번 달리는 버그가 생기므로, 번역 기능은 이 클래스 하나로 통일했다.
//    (DiscordBotService는 이 클래스를 생성자에서 주입받아 DI 컨테이너가 살아있게만 유지한다.)
public class TranslationService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<TranslationService> _logger;
    private readonly HttpClient _httpClient;

    public TranslationService(DiscordSocketClient client, ILogger<TranslationService> logger)
    {
        _client = client;
        _logger = logger;
        _httpClient = new HttpClient();
        _client.MessageReceived += OnMessageReceivedAsync;
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        // 봇이 보낸 메시지거나, 시스템 메시지면 무시
        if (message.Author.IsBot) return;

        string content = message.Content;

        if (!IsJapanese(content)) return;

        _logger.LogDebug("일본어 메시지 감지: {Author}", message.Author.Username);

        string? translatedText = await TranslateToKoreanAsync(content);

        if (!string.IsNullOrEmpty(translatedText))
        {
            string replyMessage = $"🇯🇵 **[토리의 번역기]**\n> {translatedText}";
            await message.Channel.SendMessageAsync(replyMessage, messageReference: new Discord.MessageReference(message.Id));
        }
    }

    // 일본어 문자(히라가나/가타카나)가 포함되어 있는지 판별하는 간단한 정규식
    private static bool IsJapanese(string text)
    {
        // 히라가나(\u3040-\u309F) 또는 가타카나(\u30A0-\u30FF) 유니코드 범위 체크
        return Regex.IsMatch(text, @"[\u3040-\u309F\u30A0-\u30FF]");
    }

    // 구글 번역 무료 엔드포인트를 이용한 간단한 번역 (정식 API 키 없이 동작하는 비공식 방식)
    private async Task<string?> TranslateToKoreanAsync(string text)
    {
        try
        {
            // sl: 출발어(ja=일본어), tl: 도착어(ko=한국어), q: 번역할 텍스트
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=ja&tl=ko&dt=t&q={Uri.EscapeDataString(text)}";

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("번역 요청 실패. HTTP 상태 코드: {StatusCode}", response.StatusCode);
                return null;
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();

            // 구글 번역 응답 JSON 구조 파싱 (배열 형태의 아주 원초적인 방식)
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // 첫 번째 배열 안의 번역된 텍스트 조각들을 이어붙이기
            string translatedText = "";
            var sentences = root[0];
            foreach (var sentence in sentences.EnumerateArray())
            {
                translatedText += sentence[0].GetString();
            }

            return string.IsNullOrEmpty(translatedText) ? null : translatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "번역 처리 중 예외가 발생했습니다.");
            return null;
        }
    }
}
