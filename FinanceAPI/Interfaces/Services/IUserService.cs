using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;

namespace FinanceAPI.Interfaces.Services;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, bool allowRoleChange = false, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateProfileAsync(int userId, string username, string email, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task<ApiKeyCreatedResponse> CreateApiKeyAsync(int userId, string keyName, int? createdByAdminId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(int userId, CancellationToken cancellationToken = default);
    Task RevokeApiKeyAsync(int userId, int keyId, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task AdminSetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
}
