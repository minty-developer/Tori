using MySqlConnector;

public partial class DatabaseService
{
    private static BirthdayItem ReadBirthday(MySqlDataReader reader)
    {
        return new BirthdayItem
        {
            BirthdayId = reader.GetInt32("BirthdayId"),
            Name = reader.GetString("Name"),
            Category = reader.GetString("Category"),
            Month = reader.GetByte("Month"),
            Day = reader.GetByte("Day"),
            IsEnabled = reader.GetBoolean("IsEnabled")
        };
    }

    // 전체 기념일 조회
    public async Task<List<BirthdayItem>> GetBirthdaysAsync()
    {
        const string query = """
            SELECT BirthdayId, Name, Category, Month, Day, IsEnabled
            FROM Birthdays
            ORDER BY Month, Day, Name;
            """;

        var birthdays = new List<BirthdayItem>();

        await using var connection = GetConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            birthdays.Add(ReadBirthday(reader));
        }

        return birthdays;
    }

    // ID로 특정 기념일 조회
    public async Task<BirthdayItem?> GetBirthdayAsync(int id)
    {
        const string query = """
            SELECT BirthdayId, Name, Category, Month, Day, IsEnabled
            FROM Birthdays
            WHERE BirthdayId = @BirthdayId;
            """;

        await using var connection = GetConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(query, connection);

        command.Parameters.AddWithValue("@BirthdayId", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return ReadBirthday(reader);
    }

    // 특정 날짜의 활성 기념일 조회
    public async Task<List<BirthdayItem>> GetTodayBirthdaysAsync(DateOnly date)
    {
        const string query = """
            SELECT BirthdayId, Name, Category, Month, Day, IsEnabled
            FROM Birthdays
            WHERE Month = @Month
            AND Day = @Day
            AND IsEnabled = TRUE
            ORDER BY Name;
            """;

        var birthdays = new List<BirthdayItem>();

        await using var connection = GetConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(query, connection);

        command.Parameters.AddWithValue("@Month", date.Month);
        command.Parameters.AddWithValue("@Day", date.Day);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            birthdays.Add(ReadBirthday(reader));
        }

        return birthdays;
    }

    // Upsert
    public async Task<bool> SetBirthdayAsync(BirthdayItem birthday)
    {
        if (birthday.Month is < 1 or > 12 ||
            birthday.Day < 1 ||
            birthday.Day > DateTime.DaysInMonth(2000, birthday.Month))
        {
            throw new ArgumentException("올바르지 않은 날짜입니다.");
        }

        if (birthday.Category is not ("Character" or "Engine"))
        {
            throw new ArgumentException("올바르지 않은 기념일 분류입니다.");
        }

        string query;

        if (birthday.BirthdayId == 0)
        {
            query = """
                INSERT INTO Birthdays
                    (Name, Category, Month, Day, IsEnabled)
                VALUES
                    (@Name, @Category, @Month, @Day, @IsEnabled);
                """;
        }
        else
        {
            query = """
                UPDATE Birthdays
                SET Name = @Name,
                    Category = @Category,
                    Month = @Month,
                    Day = @Day,
                    IsEnabled = @IsEnabled
                WHERE BirthdayId = @BirthdayId;
                """;
        }

        await using var connection = GetConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand(query, connection);

        command.Parameters.AddWithValue("@Name", birthday.Name);
        command.Parameters.AddWithValue("@Category", birthday.Category);
        command.Parameters.AddWithValue("@Month", birthday.Month);
        command.Parameters.AddWithValue("@Day", birthday.Day);
        command.Parameters.AddWithValue("@IsEnabled", birthday.IsEnabled);

        if (birthday.BirthdayId != 0)
        {
            command.Parameters.AddWithValue(
                "@BirthdayId", birthday.BirthdayId);
        }

        int affectedRows = await command.ExecuteNonQueryAsync();

        if (birthday.BirthdayId == 0)
        {
            birthday.BirthdayId = (int)command.LastInsertedId;
            return birthday.BirthdayId > 0;
        }

        return affectedRows > 0;
    }
}