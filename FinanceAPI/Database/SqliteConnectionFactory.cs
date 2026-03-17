using System.Data;
using Microsoft.Data.Sqlite;

namespace FinanceAPI.Database;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON";
        cmd.ExecuteNonQuery();
        return connection;
    }
}
