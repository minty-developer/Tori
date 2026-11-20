using MySqlConnector;

public partial class DatabaseService
{
    private readonly string _connectionString;

    private readonly string SQL = @"
            CREATE TABLE IF NOT EXISTS Users (
                UserId BIGINT UNSIGNED PRIMARY KEY,
                Points INT NOT NULL DEFAULT 0,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS Attendances (
                Id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                UserId BIGINT UNSIGNED NOT NULL,
                CheckInDate DATE NOT NULL,
                CheckedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                CONSTRAINT FK_Attendances_Users
                    FOREIGN KEY (UserId)
                    REFERENCES Users(UserId)
                    ON DELETE CASCADE,

                CONSTRAINT UQ_Attendances_User_Date
                    UNIQUE (UserId, CheckInDate)
            );

            CREATE TABLE IF NOT EXISTS Titles (
                TitleId INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

                Code VARCHAR(50) NOT NULL UNIQUE,
                Name VARCHAR(50) NOT NULL UNIQUE,
                Description VARCHAR(255),

                Price INT NOT NULL DEFAULT 0,

                ConditionType VARCHAR(50),
                ConditionValue INT
            );

            CREATE TABLE IF NOT EXISTS UserTitles (
                UserId BIGINT UNSIGNED NOT NULL,
                TitleId INT UNSIGNED NOT NULL,

                AcquiredAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                IsEquipped BOOLEAN NOT NULL DEFAULT FALSE,

                PRIMARY KEY (UserId, TitleId),

                FOREIGN KEY (UserId)
                    REFERENCES Users(UserId)
                    ON DELETE CASCADE,

                FOREIGN KEY (TitleId)
                    REFERENCES Titles(TitleId)
                    ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS TimelineEvents (
                EventId BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                UserId BIGINT UNSIGNED,
                EventType VARCHAR(50) NOT NULL,
                Description TEXT,
                OccurredAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                FOREIGN KEY (UserId)
                    REFERENCES Users(UserId)
                    ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS MusicQuizzes (
                QuizId BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                SongName VARCHAR(255) NOT NULL,
                Artist VARCHAR(255),
                AudioUrl TEXT NOT NULL,
                Answer VARCHAR(255) NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS QuizRecords (
                RecordId BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                QuizId BIGINT UNSIGNED NOT NULL,
                UserId BIGINT UNSIGNED NOT NULL,
                IsCorrect BOOLEAN NOT NULL,
                AnsweredAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                FOREIGN KEY (QuizId)
                    REFERENCES MusicQuizzes(QuizId)
                    ON DELETE CASCADE,

                FOREIGN KEY (UserId)
                    REFERENCES Users(UserId)
                    ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Birthdays
            (
                BirthdayId INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

                Name VARCHAR(100) NOT NULL,
                Category VARCHAR(30) NOT NULL,

                Month TINYINT UNSIGNED NOT NULL,
                Day TINYINT UNSIGNED NOT NULL,

                IsEnabled BOOLEAN NOT NULL DEFAULT TRUE,

                CONSTRAINT CHK_Birthdays_Category
                    CHECK (Category IN ('Character', 'Engine')),

                CONSTRAINT CHK_Birthdays_Month
                    CHECK (Month BETWEEN 1 AND 12),

                CONSTRAINT CHK_Birthdays_Day
                    CHECK (Day BETWEEN 1 AND 31),

                INDEX IDX_Birthdays_Date (Month, Day)
            );

            CREATE TABLE IF NOT EXISTS Polls (
                PollId BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

                Title VARCHAR(256) NOT NULL,
                CreatorId BIGINT UNSIGNED NOT NULL,

                ChannelId BIGINT UNSIGNED NULL,
                MessageId BIGINT UNSIGNED NULL,

                Status VARCHAR(20) NOT NULL DEFAULT 'Draft',

                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                StartedAt DATETIME NULL,
                EndedAt DATETIME NULL,

                IsAnonymous BOOLEAN NOT NULL DEFAULT TRUE,
                AllowRevote BOOLEAN NOT NULL DEFAULT FALSE,

                CONSTRAINT CHK_Polls_Status
                    CHECK (Status IN ('Draft', 'Active', 'Paused', 'Ended'))
            );

            CREATE TABLE IF NOT EXISTS PollOptions (
                OptionId BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

                PollId BIGINT UNSIGNED NOT NULL,
                OptionText VARCHAR(100) NOT NULL,
                Position TINYINT UNSIGNED NOT NULL,

                CONSTRAINT FK_PollOptions_Polls
                    FOREIGN KEY (PollId)
                    REFERENCES Polls(PollId)
                    ON DELETE CASCADE,

                CONSTRAINT UQ_PollOptions_Position
                    UNIQUE (PollId, Position),

                CONSTRAINT UQ_PollOptions_Id_Poll
                    UNIQUE (PollId, OptionId)
            );

            CREATE TABLE IF NOT EXISTS PollVotes (
                VoteId BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,

                PollId BIGINT UNSIGNED NOT NULL,
                OptionId BIGINT UNSIGNED NOT NULL,
                UserId BIGINT UNSIGNED NOT NULL,

                VotedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                CONSTRAINT FK_PollVotes_Polls
                    FOREIGN KEY (PollId)
                    REFERENCES Polls(PollId)
                    ON DELETE CASCADE,

                CONSTRAINT FK_PollVotes_Options
                    FOREIGN KEY (PollId, OptionId)
                    REFERENCES PollOptions(PollId, OptionId)
                    ON DELETE CASCADE,

                CONSTRAINT UQ_PollVotes_User
                    UNIQUE (PollId, UserId)
            );";

    public DatabaseService()
    {
        // 1. GetConnectionString 탐색 -> 2. 일반 환경변수 탐색 -> 3. 없으면 예외 발생
        _connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                            ?? throw new InvalidOperationException("DB 연결 문자열('DefaultConnection')을 찾을 수 없습니다. appsettings.json 또는 환경 변수를 확인하세요.");
        
        InitializeDatabase();
    }

    public MySqlConnection GetConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    private void InitializeDatabase()
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = new MySqlCommand(SQL, connection);
        command.ExecuteNonQuery();
    }
}