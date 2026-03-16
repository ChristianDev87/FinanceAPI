using System.Data;
using Dapper;

namespace FinanceAPI.Database;

public class PostgreSqlDialect : ISqlDialect
{
    public string Year(string column) => $"CAST(EXTRACT(YEAR FROM {column}::date) AS INTEGER)";
    public string Month(string column) => $"CAST(EXTRACT(MONTH FROM {column}::date) AS INTEGER)";

    public string CaseInsensitiveEqual(string column, string paramName)
        => $"LOWER({column}) = LOWER({paramName})";

    public Task<int> InsertAsync(IDbConnection conn, string sql, object param)
        => conn.QuerySingleAsync<int>(sql + " RETURNING Id", param);
}
