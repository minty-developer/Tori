using MySqlConnector;
using Microsoft.Extensions.Configuration;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        // 1. GetConnectionString 탐색 -> 2. 일반 환경변수 탐색 -> 3. 없으면 예외 발생
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? configuration["DB_CONNECTION_STRING"]
                            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
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

        // 아까 만든 MySQL 전용 테이블 생성 SQL
        string commandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                UserId BIGINT PRIMARY KEY,
                Points INT NOT NULL DEFAULT 0,
                LastCheckIn DATETIME,
                Titles JSON
            );

            CREATE TABLE IF NOT EXISTS UserFishes (
                Id INT PRIMARY KEY AUTO_INCREMENT,
                UserId BIGINT,
                FishName VARCHAR(100),
                Grade VARCHAR(20),
                Length DOUBLE,
                CatchCount INT DEFAULT 1,
                UNIQUE(UserId, FishName)
            );";

        using var command = new MySqlCommand(commandText, connection);
        command.ExecuteNonQuery();
    }
}