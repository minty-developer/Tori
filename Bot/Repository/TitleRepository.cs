using MySqlConnector;

public class TitleRepository(DatabaseService databaseService)
{
    private readonly DatabaseService _db = databaseService;

    // 전체 칭호 조회
    public async Task<List<TitleInfo>> GetAllTitlesAsync()
    {
        const string query = """
            SELECT
                TitleId,
                Code,
                Name,
                Description,
                Price,
                ConditionType,
                ConditionValue
            FROM Titles
            ORDER BY TitleId ASC;
            """;

        return await _db.QueryAsync(
            query,
            reader => new TitleInfo
            {
                TitleId = reader.GetUInt32("TitleId"),
                Code = reader.GetString("Code"),
                Name = reader.GetString("Name"),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? string.Empty
                    : reader.GetString("Description"),
                Price = reader.GetInt32("Price"),
                ConditionType = reader.IsDBNull(reader.GetOrdinal("ConditionType"))
                    ? null
                    : reader.GetString("ConditionType"),
                ConditionValue = reader.IsDBNull(reader.GetOrdinal("ConditionValue"))
                    ? null
                    : reader.GetInt32("ConditionValue")
            });
    }

    // 상점 칭호 조회
    public async Task<List<TitleInfo>> GetShopTitlesAsync()
    {
        const string query = """
            SELECT
                TitleId,
                Code,
                Name,
                Description,
                Price,
                ConditionType,
                ConditionValue
            FROM Titles
            WHERE Price > 0
            ORDER BY Price ASC;
            """;

        return await _db.QueryAsync(
            query,
            reader => new TitleInfo
            {
                TitleId = reader.GetUInt32("TitleId"),
                Code = reader.GetString("Code"),
                Name = reader.GetString("Name"),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? string.Empty
                    : reader.GetString("Description"),
                Price = reader.GetInt32("Price"),
                ConditionType = reader.IsDBNull(reader.GetOrdinal("ConditionType"))
                    ? null
                    : reader.GetString("ConditionType"),
                ConditionValue = reader.IsDBNull(reader.GetOrdinal("ConditionValue"))
                    ? null
                    : reader.GetInt32("ConditionValue")
            });
    }

    // 특정 칭호 조회
    public async Task<TitleInfo?> GetTitleAsync(string code)
    {
        const string query = """
            SELECT
                TitleId,
                Code,
                Name,
                Description,
                Price,
                ConditionType,
                ConditionValue
            FROM Titles
            WHERE Code = @Code;
            """;

        return await _db.QuerySingleAsync(
            query,
            reader => new TitleInfo
            {
                TitleId = reader.GetUInt32("TitleId"),
                Code = reader.GetString("Code"),
                Name = reader.GetString("Name"),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? string.Empty
                    : reader.GetString("Description"),
                Price = reader.GetInt32("Price"),
                ConditionType = reader.IsDBNull(reader.GetOrdinal("ConditionType"))
                    ? null
                    : reader.GetString("ConditionType"),
                ConditionValue = reader.IsDBNull(reader.GetOrdinal("ConditionValue"))
                    ? null
                    : reader.GetInt32("ConditionValue")
            },
            new[]
            {
                DatabaseService.CreateParameter("@Code", code)
            });
    }

    // 유저 칭호 조회
    public async Task<List<UserTitleInfo>> GetUserTitlesAsync(ulong userId)
    {
        const string query = """
            SELECT
                t.TitleId,
                t.Code,
                t.Name,
                t.Description,
                ut.AcquiredAt,
                ut.IsEquipped
            FROM UserTitles ut
            INNER JOIN Titles t
                ON ut.TitleId = t.TitleId
            WHERE ut.UserId = @UserId
            ORDER BY ut.AcquiredAt ASC;
            """;

        return await _db.QueryAsync(
            query,
            reader => new UserTitleInfo
            {
                TitleId = reader.GetUInt32("TitleId"),
                Code = reader.GetString("Code"),
                Name = reader.GetString("Name"),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? string.Empty
                    : reader.GetString("Description"),
                AcquiredAt = reader.GetDateTime("AcquiredAt"),
                IsEquipped = reader.GetBoolean("IsEquipped")
            },
            [
                DatabaseService.CreateParameter("@UserId", userId)
            ]);
    }

    // 유저 칭호 구매
    public async Task<PurchaseResult> PurchaseTitleAsync(
        ulong userId,
        string code)
    {
        await using var connection = _db.GetConnection();
        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            // 1. 사용자 포인트 조회 및 잠금
            const string userQuery = """
                SELECT Points
                FROM Users
                WHERE UserId = @UserId
                FOR UPDATE;
                """;

            object? userResult = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                userQuery,
                new[]
                {
                    DatabaseService.CreateParameter("@UserId", userId)
                });

            if (userResult is null)
            {
                await transaction.RollbackAsync();

                return new PurchaseResult
                {
                    Status = PurchaseStatus.UserNotFound
                };
            }

            int points = Convert.ToInt32(userResult);

            // 2. 칭호 조회
            const string titleQuery = """
                SELECT TitleId, Price
                FROM Titles
                WHERE Code = @Code;
                """;

            uint titleId;
            int price;

            await using (var command = new MySqlCommand(
                titleQuery, connection, transaction))
            {
                command.Parameters.Add(
                    DatabaseService.CreateParameter("@Code", code));

                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    await transaction.RollbackAsync();

                    return new PurchaseResult
                    {
                        Status = PurchaseStatus.TitleNotFound
                    };
                }

                titleId = reader.GetUInt32("TitleId");
                price = reader.GetInt32("Price");
            }

            // 3. 구매 가능 여부 확인
            if (price <= 0)
            {
                await transaction.RollbackAsync();

                return new PurchaseResult
                {
                    Status = PurchaseStatus.NotForSale,
                    RemainingPoints = points
                };
            }

            // 4. 이미 보유한 칭호인지 확인
            const string ownedQuery = """
                SELECT 1
                FROM UserTitles
                WHERE UserId = @UserId
                AND TitleId = @TitleId;
                """;

            object? ownedResult = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                ownedQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId),
                    DatabaseService.CreateParameter("@TitleId", titleId)
                ]);

            if (ownedResult is not null)
            {
                await transaction.RollbackAsync();

                return new PurchaseResult
                {
                    Status = PurchaseStatus.AlreadyOwned,
                    RemainingPoints = points
                };
            }

            // 5. 포인트 부족 확인
            if (points < price)
            {
                await transaction.RollbackAsync();

                return new PurchaseResult
                {
                    Status = PurchaseStatus.InsufficientPoints,
                    RemainingPoints = points
                };
            }

            // 6. 포인트 차감
            const string updateQuery = """
                UPDATE Users
                SET Points = Points - @Price
                WHERE UserId = @UserId
                AND Points >= @Price;
                """;

            int affectedRows = await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                updateQuery,
                new[]
                {
                    DatabaseService.CreateParameter("@Price", price),
                    DatabaseService.CreateParameter("@UserId", userId)
                });

            if (affectedRows != 1)
                throw new InvalidOperationException("포인트 차감에 실패했습니다.");

            // 7. 칭호 지급
            const string insertQuery = """
                INSERT INTO UserTitles
                    (UserId, TitleId)
                VALUES
                    (@UserId, @TitleId);
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                insertQuery,
                new[]
                {
                    DatabaseService.CreateParameter("@UserId", userId),
                    DatabaseService.CreateParameter("@TitleId", titleId)
                });

            // 8. 구매 완료
            await transaction.CommitAsync();

            return new PurchaseResult
            {
                Status = PurchaseStatus.Success,
                RemainingPoints = points - price
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // 유저 칭호 장착
    public async Task<bool> EquipTitleAsync(
        ulong userId,
        string code)
    {
        await using var connection = _db.GetConnection();
        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            // 1. 칭호 조회
            const string titleQuery = """
                SELECT TitleId
                FROM Titles
                WHERE Code = @Code;
                """;

            object? titleResult = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                titleQuery,
                [
                    DatabaseService.CreateParameter("@Code", code)
                ]);

            if (titleResult is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            uint titleId = Convert.ToUInt32(titleResult);

            // 2. 보유 여부 확인
            const string ownedQuery = """
                SELECT 1
                FROM UserTitles
                WHERE UserId = @UserId
                AND TitleId = @TitleId;
                """;

            object? ownedResult = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                ownedQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId),
                    DatabaseService.CreateParameter("@TitleId", titleId)
                ]);

            if (ownedResult is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            // 3. 기존 장착 칭호 해제
            const string unequipQuery = """
                UPDATE UserTitles
                SET IsEquipped = FALSE
                WHERE UserId = @UserId;
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                unequipQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId)
                ]);

            // 4. 새로운 칭호 장착
            const string equipQuery = """
                UPDATE UserTitles
                SET IsEquipped = TRUE
                WHERE UserId = @UserId
                AND TitleId = @TitleId;
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                equipQuery,
                [
                    DatabaseService.CreateParameter("@UserId", userId),
                    DatabaseService.CreateParameter("@TitleId", titleId)
                ]);

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}