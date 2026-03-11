using System.Data;
using MySqlConnector;

namespace FinanceAPI.Database;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(string connectionString)
        => _connectionString = connectionString;

    public IDbConnection CreateConnection() => new MySqlConnection(_connectionString);
}
