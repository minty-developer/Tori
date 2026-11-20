public class PollOptionInfo
{
    public ulong OptionId { get; set; }

    public ulong PollId { get; set; }

    public string OptionText { get; set; } = string.Empty;

    public byte Position { get; set; }

    public int VoteCount { get; set; }
}