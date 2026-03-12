using Dapper;
using FinanceAPI.Database;
using FinanceAPI.DTOs.Statistics;
using FinanceAPI.Interfaces.Services;

namespace FinanceAPI.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;

    public StatisticsService(IDbConnectionFactory connectionFactory, ISqlDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
    }

    public async Task<IEnumerable<int>> GetAvailableYearsAsync(int userId)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<int>(
            $"""
            SELECT DISTINCT {_dialect.Year("Date")} AS Year
            FROM Transactions
            WHERE UserId = @UserId
            ORDER BY Year DESC
            """,
            new { UserId = userId });
    }

    public async Task<IEnumerable<MonthlyStatDto>> GetMonthlyAsync(int userId, int year)
    {
        using var conn = _connectionFactory.CreateConnection();

        var rows = await conn.QueryAsync<(int Month, string Type, decimal Total)>(
            $"""
            SELECT
                {_dialect.Month("Date")} AS Month,
                Type,
                SUM(Amount) AS Total
            FROM Transactions
            WHERE UserId = @UserId
              AND {_dialect.Year("Date")} = @Year
            GROUP BY Month, Type
            ORDER BY Month
            """,
            new { UserId = userId, Year = year });

        var grouped = rows.GroupBy(r => r.Month);
        var result = new List<MonthlyStatDto>();

        for (var m = 1; m <= 12; m++)
        {
            var monthRows = grouped.FirstOrDefault(g => g.Key == m);
            result.Add(new MonthlyStatDto
            {
                Month = m,
                Year = year,
                TotalIncome = monthRows?.FirstOrDefault(r => r.Type == "income").Total ?? 0,
                TotalExpense = monthRows?.FirstOrDefault(r => r.Type == "expense").Total ?? 0
            });
        }

        return result;
    }

    public async Task<IEnumerable<CategoryStatDto>> GetByCategoryAsync(
        int userId, int month, int year, string? type)
    {
        using var conn = _connectionFactory.CreateConnection();

        var sql = $"""
            SELECT
                t.CategoryId,
                COALESCE(c.Name, 'Uncategorized') AS CategoryName,
                COALESCE(c.Color, '#95a5a6') AS Color,
                t.Type,
                SUM(t.Amount) AS Total,
                COUNT(*) AS Count
            FROM Transactions t
            LEFT JOIN Categories c ON t.CategoryId = c.Id
            WHERE t.UserId = @UserId
              AND {_dialect.Month("t.Date")} = @Month
              AND {_dialect.Year("t.Date")} = @Year
            """;

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);
        parameters.Add("Month", month);
        parameters.Add("Year", year);

        if (!string.IsNullOrEmpty(type))
        {
            sql += " AND t.Type = @Type";
            parameters.Add("Type", type);
        }

        sql += " GROUP BY t.CategoryId, c.Name, c.Color, t.Type ORDER BY Total DESC";

        return await conn.QueryAsync<CategoryStatDto>(sql, parameters);
    }
}
