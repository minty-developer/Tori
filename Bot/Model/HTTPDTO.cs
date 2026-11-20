/// <summary>lyrics_bokaro에서 오는 데이터를 받을 모델</summary>
public class AnnouncementRequest
{
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Changes { get; set; } = "";
}