using Discord.Interactions;
using Discord;

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