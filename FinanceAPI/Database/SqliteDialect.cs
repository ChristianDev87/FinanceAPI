using System.Data;
using Dapper;

namespace FinanceAPI.Database;

public class SqliteDialect : ISqlDialect
{
    public string Year(string column) => $"CAST(strftime('%Y', {column}) AS INTEGER)";
    public string Month(string column) => $"CAST(strftime('%m', {column}) AS INTEGER)";

    public string CaseInsensitiveEqual(string column, string paramName)
        => $"{column} = {paramName} COLLATE NOCASE";

    public async Task<int> InsertAsync(IDbConnection conn, string sql, object param, IDbTransaction? transaction = null)
    {
        await conn.ExecuteAsync(sql, param, transaction: transaction);
        return (int)await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid()", transaction: transaction);
    }
}
