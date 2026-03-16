using System.Data;
using FinanceAPI.Database;
using Microsoft.Data.Sqlite;

namespace FinanceAPI.Tests.Integration;

/// <summary>
/// An IDbConnectionFactory backed by a named SQLite in-memory database.
/// Every call to CreateConnection() returns a new connection to the same
/// named in-memory database (Cache=Shared), so data persists as long as
/// at least one connection is open (see FinanceApiFactory._keepAlive).
/// </summary>
public sealed class InMemoryDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public InMemoryDbConnectionFactory(string dbName)
    {
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
    }

    public IDbConnection CreateConnection()
    {
        SqliteConnection conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
