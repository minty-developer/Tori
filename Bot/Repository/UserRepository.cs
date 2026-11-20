public class UserRepository(DatabaseService databaseService)
{
    private readonly DatabaseService _db = databaseService;

    private const int AttendanceReward = 100;

    // 출석 체크
    public async Task<AttendanceResult> CheckInAsync(
        ulong userId,
        DateOnly today)
    {
        await using var connection = _db.GetConnection();
        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            // 1. 사용자 등록
            const string insertUserQuery = """
                INSERT IGNORE INTO Users (UserId)
                VALUES (@UserId);
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                insertUserQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId)
                ]);

            // 2. 출석 기록 등록
            const string insertAttendanceQuery = """
                INSERT IGNORE INTO Attendances
                    (UserId, CheckInDate)
                VALUES
                    (@UserId, @CheckInDate);
                """;

            int affectedRows = await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                insertAttendanceQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId),
                    DatabaseService.CreateParameter(
                        "@CheckInDate",
                        today.ToDateTime(TimeOnly.MinValue))
                ]);

            // 3. 이미 출석한 경우
            if (affectedRows == 0)
            {
                const string selectPointsQuery = """
                    SELECT Points
                    FROM Users
                    WHERE UserId = @UserId;
                    """;

                var result = await _db.ExecuteScalarAsync(
                    connection,
                    transaction,
                    selectPointsQuery,
                    [
                        DatabaseService.CreateParameter("@UserId", userId)
                    ]);

                int points = Convert.ToInt32(result);

                await transaction.CommitAsync();

                return new AttendanceResult
                {
                    IsAlreadyCheckedIn = true,
                    Points = points
                };
            }

            // 4. 포인트 지급
            const string updatePointsQuery = """
                UPDATE Users
                SET Points = Points + @Reward
                WHERE UserId = @UserId;
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                updatePointsQuery,
                [
                    DatabaseService.CreateParameter(
                        "@Reward", AttendanceReward),
                    DatabaseService.CreateParameter("@UserId", userId)
                ]);

            // 5. 최종 포인트 조회
            const string selectFinalPointsQuery = """
                SELECT Points
                FROM Users
                WHERE UserId = @UserId;
                """;

            var finalResult = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                selectFinalPointsQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId)
                ]);

            int finalPoints = Convert.ToInt32(finalResult);

            await transaction.CommitAsync();

            return new AttendanceResult
            {
                IsAlreadyCheckedIn = false,
                Points = finalPoints
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // 포인트 조회
    public async Task<long?> GetPointsAsync(ulong userId)
    {
        const string query = """
            SELECT Points
            FROM Users
            WHERE UserId = @UserId;
            """;

        var result = await _db.ExecuteScalarAsync(
            query,
            [
                DatabaseService.CreateParameter("@UserId", userId)
            ]);

        return result is null
            ? null
            : Convert.ToInt64(result);
    }

    // 프로필 조회
    public async Task<ProfileResult?> GetProfileAsync(ulong userId)
    {
        const string query = """
            SELECT
                u.Points,

                COALESCE(
                    (
                        SELECT t.Name
                        FROM UserTitles ut
                        INNER JOIN Titles t
                            ON ut.TitleId = t.TitleId
                        WHERE ut.UserId = u.UserId
                          AND ut.IsEquipped = TRUE
                        ORDER BY ut.AcquiredAt DESC, ut.TitleId DESC
                        LIMIT 1
                    ),
                    '[뉴비]'
                ) AS EquippedTitle,

                (
                    SELECT MAX(a.CheckInDate)
                    FROM Attendances a
                    WHERE a.UserId = u.UserId
                ) AS LastCheckIn

            FROM Users u
            WHERE u.UserId = @UserId;
            """;

        return await _db.QuerySingleAsync(
            query,
            reader => new ProfileResult
            {
                Points = reader.GetInt32("Points"),
                EquippedTitle = reader.GetString("EquippedTitle"),

                LastCheckIn = reader.IsDBNull(
                    reader.GetOrdinal("LastCheckIn"))
                    ? null
                    : DateOnly.FromDateTime(
                        reader.GetDateTime("LastCheckIn"))
            },
            [
                DatabaseService.CreateParameter("@UserId", userId)
            ]);
    }
}