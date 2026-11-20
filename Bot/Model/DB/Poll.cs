public class PollInfo
{
    public ulong PollId { get; set; }

    public string Title { get; set; } = string.Empty;

    public ulong CreatorId { get; set; }

    public ulong? ChannelId { get; set; }

    public ulong? MessageId { get; set; }

    public PollStatus Status { get; set; } = PollStatus.Draft;

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }
    public bool IsAnonymous { get; set; } = true;
    public bool AllowRevote { get; set; } = false;

    public List<PollOptionInfo> Options { get; set; } = [];
}

public enum PollStatus
{
    Draft,      // 생성만 된 상태
    Active,     // 투표 진행 중
    Paused,     // 일시정지
    Ended       // 종료
}

public enum VoteResult
{
    Success,
    PollNotActive,
    OptionNotFound,
    AlreadyVoted
}