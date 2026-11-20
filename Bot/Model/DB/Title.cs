public class TitleInfo
{
    public uint TitleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int Price { get; set; }

    public string? ConditionType { get; set; }
    public int? ConditionValue { get; set; }
}

public class UserTitleInfo
{
    public uint TitleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public DateTime AcquiredAt { get; set; }
    public bool IsEquipped { get; set; }
}