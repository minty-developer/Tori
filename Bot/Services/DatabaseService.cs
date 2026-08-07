using MySqlConnector;
using Microsoft.Extensions.Configuration;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        // 1. null을 체크하고, 없으면 명확한 에러를 발생시켜 앱이 비정상 동작하지 않게 합니다.
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        
        InitializeDatabase(); // 테이블 생성 로직도 여기서 호출하세요!
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