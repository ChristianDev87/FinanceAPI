using System.Data;
using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByHashAsync(string keyHash);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(int userId);
    Task<ApiKey?> GetByIdAsync(int id);
    Task<int> CreateAsync(ApiKey apiKey);
    Task<int> CreateAsync(ApiKey apiKey, IDbConnection conn, IDbTransaction txn);
    Task DeactivateAsync(int id);
    Task DeactivateAllForUserAsync(int userId);
    Task DeactivateAllForUserAsync(int userId, IDbConnection conn, IDbTransaction txn);
}
