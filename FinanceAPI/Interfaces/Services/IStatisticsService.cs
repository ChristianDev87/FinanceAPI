using FinanceAPI.DTOs.Statistics;

namespace FinanceAPI.Interfaces.Services;

public interface IStatisticsService
{
    Task<IEnumerable<int>> GetAvailableYearsAsync(int userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MonthlyStatDto>> GetMonthlyAsync(int userId, int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<CategoryStatDto>> GetByCategoryAsync(int userId, int month, int year, string? type, CancellationToken cancellationToken = default);
}
