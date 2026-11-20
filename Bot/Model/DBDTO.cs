public class BirthdayItem
{
    public int BirthdayId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int Month { get; set; }

    public int Day { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public class TimelineItem
{
    public long EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class AttendanceResult
{
    public bool IsAlreadyCheckedIn { get; set; }

    public int Points { get; set; }
}

public class ProfileResult
{
    public int Points { get; set; }

    public string EquippedTitle { get; set; } = "[뉴비]";

    public DateOnly? LastCheckIn { get; set; }
}

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

public class PurchaseResult
{
    public PurchaseStatus Status { get; set; }
    public int RemainingPoints { get; set; }
}

public enum PurchaseStatus
{
    Success,
    UserNotFound,
    TitleNotFound,
    AlreadyOwned,
    InsufficientPoints,
    NotForSale
}