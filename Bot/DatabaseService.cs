using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

// SQLite 연결 문자열 관리 및 최초 실행 시 테이블 스키마를 준비하는 서비스.
// 다른 커맨드 클래스들은 이 서비스의 GetConnection()으로 커넥션을 하나씩 받아서 사용한다.
public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;

        // 실행 파일 경로 근처에 bot.db 파일 생성
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bot.db");
        _connectionString = $"Data Source={dbPath}";

        InitializeDatabase();
    }

    /// <summary>
    /// 새 SQLite 커넥션을 생성해서 반환한다. 호출한 쪽에서 Open()/Dispose()를 책임진다.
    /// </summary>
    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();

            // 유저 포인트를 저장할 테이블 (디스코드 유저 ID, 포인트, 마지막 출석일, 보유/장착 칭호 JSON)
            // 🐟 낚시 도감 테이블도 함께 생성한다.
            string commandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INTEGER PRIMARY KEY,
                    Points INTEGER NOT NULL DEFAULT 0,
                    LastCheckIn TEXT,
                    Titles TEXT
                );

                CREATE TABLE IF NOT EXISTS UserFishes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    FishName TEXT,
                    Grade TEXT,
                    Length REAL,
                    CatchCount INTEGER DEFAULT 1,
                    UNIQUE(UserId, FishName)
                );
            ";

            using var command = new SqliteCommand(commandText, connection);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // 💡 발생 상황: DB 파일 경로에 쓰기 권한이 없거나, 파일이 손상되어 SQLite가 열지 못할 때.
            //    여기서 실패하면 봇의 모든 DB 관련 기능이 동작하지 않으므로 반드시 로그를 남긴다.
            _logger.LogError(ex, "DB 초기화 중 오류가 발생했습니다.");
            throw;
        }
    }
}
