using System.Data;

namespace FinanceAPI.Database;

/// <summary>
/// Abstracts database-specific SQL expressions so repositories and services
/// stay provider-agnostic. Register one implementation per provider in Program.cs.
/// </summary>
public interface ISqlDialect
{
    /// <summary>SQL expression that extracts the year as an integer from a date/datetime column.</summary>
    string Year(string column);

    /// <summary>SQL expression that extracts the month as an integer from a date/datetime column.</summary>
    string Month(string column);

    /// <summary>Case-insensitive equality check suitable for a WHERE clause.</summary>
    string CaseInsensitiveEqual(string column, string paramName);

    /// <summary>Executes an INSERT statement and returns the newly generated row ID.</summary>
    Task<int> InsertAsync(IDbConnection conn, string sql, object param);
}
