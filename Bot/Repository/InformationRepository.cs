public class InformationRepository(DatabaseService databaseService)
{
    private readonly DatabaseService _db = databaseService;

    public async Task<List<TimelineItem>> GetTimelineAsync()
    {
        const string query = """
            SELECT
                EventId,
                EventType,
                Description,
                OccurredAt
            FROM TimelineEvents
            ORDER BY OccurredAt ASC;
            """;

        return await _db.QueryAsync(
            query,
            reader => new TimelineItem
            {
                EventId = reader.GetInt64("EventId"),
                EventType = reader.GetString("EventType"),
                Description = reader.IsDBNull(
                    reader.GetOrdinal("Description"))
                    ? string.Empty
                    : reader.GetString("Description"),
                OccurredAt = reader.GetDateTime("OccurredAt")
            });
    }
}