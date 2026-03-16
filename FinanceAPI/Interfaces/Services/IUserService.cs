using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;

namespace FinanceAPI.Interfaces.Services;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto> GetByIdAsync(int id);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, bool allowRoleChange = false);
    Task DeleteAsync(int id);
    Task SetActiveAsync(int id, bool isActive);
    Task<ApiKeyCreatedResponse> CreateApiKeyAsync(int userId, string keyName, int? createdByAdminId = null);
    Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(int userId);
    Task RevokeApiKeyAsync(int userId, int keyId);
}
