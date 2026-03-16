using System.Data;
using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(ApiKey apiKey, IDbConnection conn, IDbTransaction txn);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task DeactivateAllForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task DeactivateAllForUserAsync(int userId, IDbConnection conn, IDbTransaction txn);
}
