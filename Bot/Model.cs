namespace model;

/// <summary>Birthday.json 한 줄에 대응하는 생일 정보.</summary>
public class BirthdayItem
{
    public int Month { get; set; }
    public int Day { get; set; }
    public string Name { get; set; } = "";
    public string Link { get; set; } = "";
}

/// <summary>timeline.json 한 줄에 대응하는 서버 연혁 항목.</summary>
public class TimelineItem
{
    public string Date { get; set; } = "";        // "2026-05-15"
    public string Title { get; set; } = "";       // 사건 제목
    public string Description { get; set; } = ""; // 사건 설명
}

/// <summary>titles.json 한 항목(칭호 정의)에 대응.</summary>
public class TitleInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Price { get; set; } = 0;
    public string Condition { get; set; } = "";
}

/// <summary>Users 테이블의 Titles 컬럼(JSON)을 역/직렬화할 때 쓰는 모델.</summary>
public class UserTitleData
{
    // 현재 장착 중인 칭호 키 (예: "Newbie")
    public string EquippedTitleKey { get; set; } = "Newbie";

    // 보유 중인 칭호 키 목록 (예: ["Newbie", "MorningBird"])
    public List<string> OwnedTitleKeys { get; set; } = new List<string> { "Newbie" };
}

/// <summary>songs.json 한 항목(보카로 퀴즈 문제)에 대응.</summary>
public class SongQuizModel
{
    public string? Title { get; set; }
    public string? HintType { get; set; }
    public string? Hint { get; set; }
    public string? Description { get; set; }
}
