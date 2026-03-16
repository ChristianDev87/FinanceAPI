using System.Security.Cryptography;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;

namespace FinanceAPI.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IApiKeyRepository _apiKeyRepo;

    public UserService(IUserRepository userRepo, IApiKeyRepository apiKeyRepo)
    {
        _userRepo = userRepo;
        _apiKeyRepo = apiKeyRepo;
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
        await _userRepo.DeleteAsync(user.Id);
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        _ = await _userRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");
        await _userRepo.SetActiveAsync(id, isActive);
    }

    public async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(int userId, string keyName, int? createdByAdminId = null)
    {
        _ = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await _apiKeyRepo.DeactivateAllForUserAsync(userId);

        byte[] rawBytes = RandomNumberGenerator.GetBytes(32);
        string rawKey = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey));
        string keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        ApiKey apiKey = new ApiKey
        {
            UserId = userId,
            KeyHash = keyHash,
            Name = keyName,
            IsActive = true,
            CreatedByAdminId = createdByAdminId
        };

        int id = await _apiKeyRepo.CreateAsync(apiKey);
        ApiKey created = await _apiKeyRepo.GetByIdAsync(id)
                        ?? throw new InvalidOperationException($"Failed to retrieve newly created API key {id}.");

        return new ApiKeyCreatedResponse
        {
            Id = id,
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
