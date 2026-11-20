public class VoteRepository(DatabaseService databaseService)
{
    private readonly DatabaseService _db = databaseService;

    // ==================================================
    // Validation
    // ==================================================

    private static void ValidatePollInput(
        string title,
        List<string> options)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("투표 제목은 비어 있을 수 없습니다.");

        if (title.Length > 256)
            throw new ArgumentException("투표 제목은 256자 이하여야 합니다.");

        if (options is null || options.Count < 2 || options.Count > 5)
            throw new ArgumentException("선택지는 2~5개여야 합니다.");

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option))
                throw new ArgumentException("선택지는 비어 있을 수 없습니다.");

            if (option.Length > 100)
                throw new ArgumentException("선택지는 100자 이하여야 합니다.");
        }
    }


    // ==================================================
    // Create
    // ==================================================

    public async Task<ulong> CreatePollAsync(
        ulong creatorId,
        string title,
        List<string> options,
        bool isAnonymous,
        bool allowRevote)
    {
        ValidatePollInput(title, options);

        using var connection = _db.GetConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. 투표 기본 정보 생성
            const string pollQuery = """
                INSERT INTO Polls
                (
                    Title,
                    CreatorId,
                    IsAnonymous,
                    AllowRevote
                )
                VALUES
                (
                    @Title,
                    @CreatorId,
                    @IsAnonymous,
                    @AllowRevote
                );
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                pollQuery,
                [
                    DatabaseService.CreateParameter("@Title", title),
                    DatabaseService.CreateParameter("@CreatorId", creatorId),
                    DatabaseService.CreateParameter("@IsAnonymous", isAnonymous),
                    DatabaseService.CreateParameter("@AllowRevote", allowRevote)
                ]);

            // 2. 생성된 투표 번호 조회
            const string idQuery = "SELECT LAST_INSERT_ID();";

            var result = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                idQuery);

            ulong pollId = Convert.ToUInt64(result);

            // 3. 선택지 생성
            const string optionQuery = """
                INSERT INTO PollOptions
                (
                    PollId,
                    OptionText,
                    Position
                )
                VALUES
                (
                    @PollId,
                    @OptionText,
                    @Position
                );
                """;

            for (byte i = 0; i < options.Count; i++)
            {
                await _db.ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    optionQuery,
                    [
                        DatabaseService.CreateParameter("@PollId", pollId),
                        DatabaseService.CreateParameter("@OptionText", options[i]),
                        DatabaseService.CreateParameter("@Position", i + 1)
                    ]);
            }

            // 4. 트랜잭션 커밋
            await transaction.CommitAsync();

            return pollId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    // ==================================================
    // Read
    // ==================================================

    public async Task<PollInfo?> GetPollAsync(ulong pollId)
    {
        const string query = """
            SELECT
                P.PollId,
                P.Title,
                P.CreatorId,
                P.ChannelId,
                P.MessageId,
                P.Status,
                P.CreatedAt,
                P.StartedAt,
                P.EndedAt,
                P.IsAnonymous,
                P.AllowRevote
            FROM Polls P
            WHERE P.PollId = @PollId;
            """;

        var poll = await _db.QuerySingleAsync(
            query,
            reader => new PollInfo
            {
                PollId = Convert.ToUInt64(reader["PollId"]),
                Title = reader["Title"].ToString()!,
                CreatorId = Convert.ToUInt64(reader["CreatorId"]),

                ChannelId = reader["ChannelId"] == DBNull.Value
                    ? null
                    : Convert.ToUInt64(reader["ChannelId"]),

                MessageId = reader["MessageId"] == DBNull.Value
                    ? null
                    : Convert.ToUInt64(reader["MessageId"]),

                Status = Enum.Parse<PollStatus>(
                    reader["Status"].ToString()!),

                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),

                StartedAt = reader["StartedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["StartedAt"]),

                EndedAt = reader["EndedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["EndedAt"]),

                IsAnonymous = Convert.ToBoolean(reader["IsAnonymous"]),
                AllowRevote = Convert.ToBoolean(reader["AllowRevote"]),
            },
            [
                DatabaseService.CreateParameter("@PollId", pollId)
            ]);

        if (poll is null)
            return null;

        const string optionQuery = """
            SELECT
                O.OptionId,
                O.PollId,
                O.OptionText,
                O.Position,
                COUNT(V.VoteId) AS VoteCount
            FROM PollOptions O
            LEFT JOIN PollVotes V
                ON O.PollId = V.PollId
                AND O.OptionId = V.OptionId
            WHERE O.PollId = @PollId
            GROUP BY
                O.OptionId,
                O.PollId,
                O.OptionText,
                O.Position
            ORDER BY O.Position;
            """;

        poll.Options = await _db.QueryAsync(
            optionQuery,
            reader => new PollOptionInfo
            {
                OptionId = Convert.ToUInt64(reader["OptionId"]),
                PollId = Convert.ToUInt64(reader["PollId"]),
                OptionText = reader["OptionText"].ToString()!,
                Position = Convert.ToByte(reader["Position"]),
                VoteCount = Convert.ToInt32(reader["VoteCount"])
            },
            [
                DatabaseService.CreateParameter("@PollId", pollId)
            ]);

        return poll;
    }


    public async Task<List<PollInfo>> GetPollsAsync(
        PollStatus? status = null)
    {
        const string query = """
            SELECT
                PollId,
                Title,
                CreatorId,
                ChannelId,
                MessageId,
                Status,
                CreatedAt,
                StartedAt,
                EndedAt,
                IsAnonymous,
                AllowRevote
            FROM Polls
            WHERE @Status IS NULL OR Status = @Status
            ORDER BY PollId DESC;
            """;

        return await _db.QueryAsync(
            query,
            reader => new PollInfo
            {
                PollId = Convert.ToUInt64(reader["PollId"]),
                Title = reader["Title"].ToString()!,
                CreatorId = Convert.ToUInt64(reader["CreatorId"]),

                ChannelId = reader["ChannelId"] == DBNull.Value
                    ? null
                    : Convert.ToUInt64(reader["ChannelId"]),

                MessageId = reader["MessageId"] == DBNull.Value
                    ? null
                    : Convert.ToUInt64(reader["MessageId"]),

                Status = Enum.Parse<PollStatus>(
                    reader["Status"].ToString()!),

                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),

                StartedAt = reader["StartedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["StartedAt"]),

                EndedAt = reader["EndedAt"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["EndedAt"]),

                IsAnonymous = Convert.ToBoolean(reader["IsAnonymous"]),
                AllowRevote = Convert.ToBoolean(reader["AllowRevote"]),
            },
            [
                DatabaseService.CreateParameter(
                    "@Status",
                    status?.ToString())
            ]);
    }


    // ==================================================
    // Update
    // ==================================================

    public async Task<bool> UpdateDraftPollAsync(
        ulong pollId,
        string title,
        List<string> options)
    {
        ValidatePollInput(title, options);

        using var connection = _db.GetConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string updateQuery = """
                UPDATE Polls
                SET Title = @Title
                WHERE PollId = @PollId
                    AND Status = 'Draft';
                """;

            int affectedRows = await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                updateQuery,
                [
                    DatabaseService.CreateParameter("@Title", title),
                    DatabaseService.CreateParameter("@PollId", pollId)
                ]);

            // UPDATE 결과가 0이어도 실제 투표가 존재할 수 있음.
            // 동일한 제목을 입력한 경우가 대표적인 예시.
            if (affectedRows == 0)
            {
                const string existsQuery = """
                    SELECT PollId
                    FROM Polls
                    WHERE PollId = @PollId
                        AND Status = 'Draft';
                    """;

                var result = await _db.ExecuteScalarAsync(
                    connection,
                    transaction,
                    existsQuery,
                    [
                        DatabaseService.CreateParameter("@PollId", pollId)
                    ]);

                if (result is null || result == DBNull.Value)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            const string deleteQuery = """
                DELETE FROM PollOptions
                WHERE PollId = @PollId;
                """;

            await _db.ExecuteNonQueryAsync(
                connection,
                transaction,
                deleteQuery,
                [
                    DatabaseService.CreateParameter("@PollId", pollId)
                ]);

            const string insertQuery = """
                INSERT INTO PollOptions (PollId, OptionText, Position)
                VALUES (@PollId, @OptionText, @Position);
                """;

            for (byte i = 0; i < options.Count; i++)
            {
                await _db.ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    insertQuery,
                    [
                        DatabaseService.CreateParameter("@PollId", pollId),
                        DatabaseService.CreateParameter("@OptionText", options[i]),
                        DatabaseService.CreateParameter("@Position", i + 1)
                    ]);
            }

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> DeleteDraftPollAsync(ulong pollId)
    {
        const string query = """
            DELETE FROM Polls
            WHERE PollId = @PollId
                AND Status = 'Draft';
            """;

        int affectedRows = await _db.ExecuteNonQueryAsync(
            query,
            [
                DatabaseService.CreateParameter("@PollId", pollId)
            ]);

        return affectedRows > 0;
    }


    // ==================================================
    // Status
    // ==================================================

    public async Task<bool> UpdatePollStatusAsync(
        ulong pollId,
        PollStatus newStatus)
    {
        if (!Enum.IsDefined(typeof(PollStatus), newStatus))
            throw new ArgumentException("올바르지 않은 투표 상태입니다.");

        const string query = """
            UPDATE Polls
            SET
                Status = @NewStatus,

                StartedAt = CASE
                    WHEN @NewStatus = 'Active' AND StartedAt IS NULL
                    THEN CURRENT_TIMESTAMP
                    ELSE StartedAt
                END,

                EndedAt = CASE
                    WHEN @NewStatus = 'Ended'
                    THEN CURRENT_TIMESTAMP
                    ELSE EndedAt
                END

            WHERE PollId = @PollId
                AND (
                    (Status = 'Draft' AND @NewStatus = 'Active')
                    OR (Status = 'Active' AND @NewStatus IN ('Paused', 'Ended'))
                    OR (Status = 'Paused' AND @NewStatus IN ('Active', 'Ended'))
                );
            """;

        int affectedRows = await _db.ExecuteNonQueryAsync(
            query,
            [
                DatabaseService.CreateParameter("@NewStatus", newStatus.ToString()),
                DatabaseService.CreateParameter("@PollId", pollId)
            ]);

        return affectedRows > 0;
    }


    // ==================================================
    // Message
    // ==================================================

    public async Task<bool> SetPollMessageAsync(
        ulong pollId,
        ulong channelId,
        ulong messageId)
    {
        const string query = """
            UPDATE Polls
            SET
                ChannelId = @ChannelId,
                MessageId = @MessageId
            WHERE PollId = @PollId
                AND Status = 'Draft';
            """;

        int affectedRows = await _db.ExecuteNonQueryAsync(
            query,
            [
                DatabaseService.CreateParameter("@PollId", pollId),
                DatabaseService.CreateParameter("@ChannelId", channelId),
                DatabaseService.CreateParameter("@MessageId", messageId)
            ]);

        return affectedRows > 0;
    }


    // ==================================================
    // Voting
    // ==================================================

    public async Task<VoteResult> RegisterVoteAsync(
        ulong pollId,
        ulong optionId,
        ulong userId)
    {
        using var connection = _db.GetConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // ==============================================
            // 1. 투표 상태 및 재투표 설정 확인
            // ==============================================

            const string pollQuery = """
                SELECT Status, AllowRevote
                FROM Polls
                WHERE PollId = @PollId
                FOR UPDATE;
                """;

            await using var pollCommand = connection.CreateCommand();

            pollCommand.Transaction = transaction;
            pollCommand.CommandText = pollQuery;

            pollCommand.Parameters.Add(
                DatabaseService.CreateParameter("@PollId", pollId));

            string? status;
            bool allowRevote;

            await using (var reader = await pollCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    await transaction.RollbackAsync();
                    return VoteResult.PollNotActive;
                }

                status = reader["Status"].ToString();
                allowRevote = Convert.ToBoolean(reader["AllowRevote"]);
            }

            if (status != PollStatus.Active.ToString())
            {
                await transaction.RollbackAsync();
                return VoteResult.PollNotActive;
            }


            // ==============================================
            // 2. 선택지가 해당 투표에 속하는지 확인
            // ==============================================

            const string optionQuery = """
                SELECT OptionId
                FROM PollOptions
                WHERE PollId = @PollId
                    AND OptionId = @OptionId;
                """;

            var optionResult = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                optionQuery,
                [
                    DatabaseService.CreateParameter("@PollId", pollId),
                    DatabaseService.CreateParameter("@OptionId", optionId)
                ]);

            if (optionResult is null || optionResult == DBNull.Value)
            {
                await transaction.RollbackAsync();
                return VoteResult.OptionNotFound;
            }


            // ==============================================
            // 3. 기존 투표 확인
            // ==============================================

            const string existingVoteQuery = """
                SELECT VoteId
                FROM PollVotes
                WHERE PollId = @PollId
                    AND UserId = @UserId;
                """;

            var existingVote = await _db.ExecuteScalarAsync(
                connection,
                transaction,
                existingVoteQuery,
                [
                    DatabaseService.CreateParameter("@PollId", pollId),
                    DatabaseService.CreateParameter("@UserId", userId)
                ]);

            bool hasVoted =
                existingVote is not null &&
                existingVote != DBNull.Value;


            // ==============================================
            // 4. 기존 투표가 있는 경우
            // ==============================================

            if (hasVoted)
            {
                if (!allowRevote)
                {
                    await transaction.RollbackAsync();
                    return VoteResult.AlreadyVoted;
                }

                const string updateQuery = """
                    UPDATE PollVotes
                    SET
                        OptionId = @OptionId,
                        VotedAt = CURRENT_TIMESTAMP
                    WHERE PollId = @PollId
                        AND UserId = @UserId;
                    """;

                await _db.ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    updateQuery,
                    [
                        DatabaseService.CreateParameter("@OptionId", optionId),
                        DatabaseService.CreateParameter("@PollId", pollId),
                        DatabaseService.CreateParameter("@UserId", userId)
                    ]);
            }

            // ==============================================
            // 5. 최초 투표인 경우
            // ==============================================

            else
            {
                const string insertQuery = """
                    INSERT INTO PollVotes (
                        PollId,
                        OptionId,
                        UserId
                    )
                    VALUES (
                        @PollId,
                        @OptionId,
                        @UserId
                    );
                    """;

                await _db.ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    insertQuery,
                    [
                        DatabaseService.CreateParameter("@PollId", pollId),
                        DatabaseService.CreateParameter("@OptionId", optionId),
                        DatabaseService.CreateParameter("@UserId", userId)
                    ]);
            }

            await transaction.CommitAsync();

            return VoteResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<PollVoteInfo?> GetUserVoteAsync(
        ulong pollId,
        ulong userId)
    {
        const string query = """
            SELECT
                VoteId,
                PollId,
                OptionId,
                UserId,
                VotedAt
            FROM PollVotes
            WHERE PollId = @PollId
                AND UserId = @UserId;
            """;

        return await _db.QuerySingleAsync(
            query,
            reader => new PollVoteInfo
            {
                VoteId = Convert.ToUInt64(reader["VoteId"]),
                PollId = Convert.ToUInt64(reader["PollId"]),
                OptionId = Convert.ToUInt64(reader["OptionId"]),
                UserId = Convert.ToUInt64(reader["UserId"]),
                VotedAt = Convert.ToDateTime(reader["VotedAt"])
            },
            [
                DatabaseService.CreateParameter("@PollId", pollId),
                DatabaseService.CreateParameter("@UserId", userId)
            ]);
    }


    // ==================================================
    // Results
    // ==================================================

    public async Task<List<PollOptionInfo>> GetPollResultsAsync(
        ulong pollId)
    {
        const string query = """
            SELECT
                O.OptionId,
                O.PollId,
                O.OptionText,
                O.Position,
                COUNT(V.VoteId) AS VoteCount
            FROM PollOptions O
            LEFT JOIN PollVotes V
                ON O.PollId = V.PollId
                AND O.OptionId = V.OptionId
            WHERE O.PollId = @PollId
            GROUP BY
                O.OptionId,
                O.PollId,
                O.OptionText,
                O.Position
            ORDER BY O.Position;
            """;

        return await _db.QueryAsync(
            query,
            reader => new PollOptionInfo
            {
                OptionId = Convert.ToUInt64(reader["OptionId"]),
                PollId = Convert.ToUInt64(reader["PollId"]),
                OptionText = reader["OptionText"].ToString()!,
                Position = Convert.ToByte(reader["Position"]),
                VoteCount = Convert.ToInt32(reader["VoteCount"])
            },
            [
                DatabaseService.CreateParameter("@PollId", pollId)
            ]);
    }
}