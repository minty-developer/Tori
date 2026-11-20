public class PollVoteInfo
{
    public ulong VoteId { get; set; }

    public ulong PollId { get; set; }

    public ulong OptionId { get; set; }

    public ulong UserId { get; set; }

    public DateTime VotedAt { get; set; }
}