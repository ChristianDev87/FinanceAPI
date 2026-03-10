using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByHashAsync(string keyHash);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(int userId);
    Task<ApiKey?> GetByIdAsync(int id);
    Task<int> CreateAsync(ApiKey apiKey);
    Task DeactivateAsync(int id);
    Task DeactivateAllForUserAsync(int userId);
}
