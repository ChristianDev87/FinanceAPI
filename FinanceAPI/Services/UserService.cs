using System.Data;
using System.Security.Cryptography;
using FinanceAPI.Database;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;

namespace FinanceAPI.Services;

public class UserService : IUserService
{
    // Serialises all operations that can reduce the active-admin count so that
    // the "at least one active admin" invariant cannot be broken under parallelism.
    // Note: single-process only — a distributed lock would be required for multi-node.
    private static readonly SemaphoreSlim _adminMutationLock = new(1, 1);

    private readonly IUserRepository _userRepo;
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IDbConnectionFactory _connectionFactory;

    public UserService(IUserRepository userRepo, IApiKeyRepository apiKeyRepo, IDbConnectionFactory connectionFactory)
    {
        _userRepo = userRepo;
        _apiKeyRepo = apiKeyRepo;
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        IEnumerable<User> users = await _userRepo.GetAllAsync();
        return users.Select(MapToDto);
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        User user = await _userRepo.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException($"User {id} not found.");
        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, bool allowRoleChange = false)
    {
        User user = await _userRepo.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException($"User {id} not found.");

        // Check username uniqueness (excluding current user)
        User? existing = await _userRepo.GetByUsernameAsync(request.Username);
        if (existing is not null && existing.Id != id)
        {
            throw new ArgumentException("Username already taken.");
        }

        User? existingEmail = await _userRepo.GetByEmailAsync(request.Email);
        if (existingEmail is not null && existingEmail.Id != id)
        {
            throw new ArgumentException("Email already in use.");
        }

        if (allowRoleChange && user.RoleName == "Admin" && user.IsActive && request.Role != "Admin")
        {
            await _adminMutationLock.WaitAsync();
            try
            {
                int activeAdmins = await _userRepo.CountActiveAdminsAsync();
                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException("Cannot demote the last active admin.");
                }

                user.Username = request.Username;
                user.Email = request.Email;
                user.RoleName = request.Role;
                await _userRepo.UpdateAsync(user);
            }
            finally
            {
                _adminMutationLock.Release();
            }

            return MapToDto(user);
        }

        user.Username = request.Username;
        user.Email = request.Email;
        user.RoleName = allowRoleChange ? request.Role : user.RoleName;

        await _userRepo.UpdateAsync(user);
        return MapToDto(user);
    }

    public async Task DeleteAsync(int id)
    {
        User user = await _userRepo.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException($"User {id} not found.");

        if (user.RoleName == "Admin" && user.IsActive)
        {
            await _adminMutationLock.WaitAsync();
            try
            {
                int activeAdmins = await _userRepo.CountActiveAdminsAsync();
                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last active admin.");
                }

                await _userRepo.DeleteAsync(user.Id);
            }
            finally
            {
                _adminMutationLock.Release();
            }

            return;
        }

        await _userRepo.DeleteAsync(user.Id);
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        User user = await _userRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");

        if (!isActive && user.RoleName == "Admin" && user.IsActive)
        {
            await _adminMutationLock.WaitAsync();
            try
            {
                int activeAdmins = await _userRepo.CountActiveAdminsAsync();
                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException("Cannot deactivate the last active admin.");
                }

                await _userRepo.SetActiveAsync(id, isActive);
            }
            finally
            {
                _adminMutationLock.Release();
            }

            return;
        }

        await _userRepo.SetActiveAsync(id, isActive);
    }

    public async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(int userId, string keyName, int? createdByAdminId = null)
    {
        _ = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        byte[] rawBytes = RandomNumberGenerator.GetBytes(32);
        string rawKey = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        byte[] hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey));
        string keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        ApiKey apiKey = new ApiKey
        {
            UserId = userId,
            KeyHash = keyHash,
            Name = keyName,
            IsActive = true,
            CreatedByAdminId = createdByAdminId
        };

        int newKeyId;
        using (IDbConnection conn = _connectionFactory.CreateConnection())
        {
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            using IDbTransaction txn = conn.BeginTransaction();
            try
            {
                await _apiKeyRepo.DeactivateAllForUserAsync(userId, conn, txn);
                newKeyId = await _apiKeyRepo.CreateAsync(apiKey, conn, txn);
                txn.Commit();
            }
            catch
            {
                txn.Rollback();
                throw;
            }
        }

        ApiKey created = await _apiKeyRepo.GetByIdAsync(newKeyId)
                        ?? throw new InvalidOperationException($"Failed to retrieve newly created API key {newKeyId}.");

        return new ApiKeyCreatedResponse
        {
            Id = newKeyId,
            Name = keyName,
            Key = rawKey,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(int userId)
    {
        _ = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        IEnumerable<ApiKey> keys = await _apiKeyRepo.GetByUserIdAsync(userId);
        return keys.Select(k => new ApiKeyDto
        {
            Id = k.Id,
            UserId = k.UserId,
            Name = k.Name,
            IsActive = k.IsActive,
            CreatedAt = k.CreatedAt,
            CreatedByAdminId = k.CreatedByAdminId
        });
    }

    public async Task RevokeApiKeyAsync(int userId, int keyId)
    {
        ApiKey key = await _apiKeyRepo.GetByIdAsync(keyId)
                  ?? throw new KeyNotFoundException($"API key {keyId} not found.");

        if (key.UserId != userId)
        {
            throw new UnauthorizedAccessException("API key does not belong to this user.");
        }

        await _apiKeyRepo.DeactivateAsync(keyId);
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        User user = await _userRepo.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        await _userRepo.UpdatePasswordAsync(userId, BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12));
    }

    public async Task AdminSetPasswordAsync(int userId, string newPassword)
    {
        _ = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await _userRepo.UpdatePasswordAsync(userId, BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12));
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        Role = u.RoleName,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
