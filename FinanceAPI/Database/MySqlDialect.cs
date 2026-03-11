using System.Data;
using Dapper;

namespace FinanceAPI.Database;

public class MySqlDialect : ISqlDialect
{
    public string Year(string column)  => $"YEAR(STR_TO_DATE({column}, '%Y-%m-%d'))";
    public string Month(string column) => $"MONTH(STR_TO_DATE({column}, '%Y-%m-%d'))";

    public string CaseInsensitiveEqual(string column, string paramName)
        => $"LOWER({column}) = LOWER({paramName})";

    public async Task<int> InsertAsync(IDbConnection conn, string sql, object param)
    {
        await conn.ExecuteAsync(sql, param);
        return (int)await conn.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");
    }
}
