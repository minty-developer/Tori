using MySqlConnector;

public partial class DatabaseService
{
    // SQL 명령 생성
    private static MySqlCommand CreateCommand(
        MySqlConnection connection,
        string query,
        IEnumerable<MySqlParameter>? parameters = null)
    {
        var command = new MySqlCommand(query, connection);

        if (parameters is not null)
            command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    // SQL 매개변수 생성
    public static MySqlParameter CreateParameter(
        string name,
        object? value)
    {
        return new MySqlParameter(name, value ?? DBNull.Value);
    }

    // INSERT, UPDATE, DELETE 등의 SQL 실행
    public async Task<int> ExecuteNonQueryAsync(
        string query,
        IEnumerable<MySqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(
            connection, query, parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // 트랜잭션을 사용하는 SQL 실행
    public async Task<int> ExecuteNonQueryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string query,
        IEnumerable<MySqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(
            connection, query, parameters);

        command.Transaction = transaction;

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // 단일 값 반환 SQL 실행
    public async Task<object?> ExecuteScalarAsync(
        string query,
        IEnumerable<MySqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(
            connection, query, parameters);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull ? null : result;
    }

    // 트랜잭션을 사용하는 단일 값 조회
    public async Task<object?> ExecuteScalarAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string query,
        IEnumerable<MySqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(
            connection, query, parameters);

        command.Transaction = transaction;

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull ? null : result;
    }

    // 여러 행 조회
    public async Task<List<T>> QueryAsync<T>(
        string query,
        Func<MySqlDataReader, T> mapper,
        IEnumerable<MySqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();

        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(
            connection, query, parameters);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            results.Add(mapper(reader));

        return results;
    }

    // 단일 행 조회
    public async Task<T?> QuerySingleAsync<T>(
        string query,
        Func<MySqlDataReader, T> mapper,
        IEnumerable<MySqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(
            connection, query, parameters);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return mapper(reader);
    }
}