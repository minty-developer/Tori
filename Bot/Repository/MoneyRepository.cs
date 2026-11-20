public class MoneyRepository(DatabaseService databaseService)
{
    private readonly DatabaseService _db = databaseService;

    // 현재 포인트 조회
    public async Task<int> GetPointsAsync(ulong userId)
    {
        const string query = """
            SELECT Points
            FROM Users
            WHERE UserId = @UserId;
            """;

        object? result = await _db.ExecuteScalarAsync(
            query,
            [
                DatabaseService.CreateParameter("@UserId", userId)
            ]);

        return result is null ? 0 : Convert.ToInt32(result);
    }

    // 포인트 지급
    public async Task AddPointsAsync(ulong userId, int amount)
    {
        const string query = """
            INSERT INTO Users (UserId, Points)
            VALUES (@UserId, @Amount)
            ON DUPLICATE KEY UPDATE
                Points = Points + @Amount;
            """;

        await _db.ExecuteNonQueryAsync(
            query,
            new[]
            {
                DatabaseService.CreateParameter("@UserId", userId),
                DatabaseService.CreateParameter("@Amount", amount)
            });
    }

    // 포인트 차감
    public async Task RemovePointsAsync(ulong userId, int amount)
    {
        const string query = """
            INSERT INTO Users (UserId, Points)
            VALUES (@UserId, 0)
            ON DUPLICATE KEY UPDATE
                Points = GREATEST(0, Points - @Amount);
            """;

        await _db.ExecuteNonQueryAsync(
            query,
            new[]
            {
                DatabaseService.CreateParameter("@UserId", userId),
                DatabaseService.CreateParameter("@Amount", amount)
            });
    }

    // 포인트 강제 설정
    public async Task SetPointsAsync(ulong userId, int amount)
    {
        const string query = """
            INSERT INTO Users (UserId, Points)
            VALUES (@UserId, @Amount)
            ON DUPLICATE KEY UPDATE
                Points = @Amount;
            """;

        await _db.ExecuteNonQueryAsync(
            query,
            new[]
            {
                DatabaseService.CreateParameter("@UserId", userId),
                DatabaseService.CreateParameter("@Amount", amount)
            });
    }
}