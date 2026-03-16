using System.Data;
using System.Globalization;
using Dapper;

namespace FinanceAPI.Database;

/// <summary>
/// Dapper type handler for <see cref="DateOnly"/>.
/// All three DB providers (SQLite, PostgreSQL, MySQL) store the date as a TEXT/VARCHAR
/// in "yyyy-MM-dd" format, so a single string-based handler covers all of them.
/// </summary>
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public override DateOnly Parse(object value)
    {
        return value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateOnly.")
        };
    }
}
