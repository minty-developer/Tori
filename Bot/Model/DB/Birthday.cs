public class BirthdayItem
{
    public int BirthdayId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int Month { get; set; }

    public int Day { get; set; }

    public bool IsEnabled { get; set; } = true;
}