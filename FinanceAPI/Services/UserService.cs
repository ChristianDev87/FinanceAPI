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
        var users = await _userRepo.GetAllAsync();
        return users.Select(MapToDto);
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await _userRepo.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException($"User {id} not found.");
        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request)
    {
        var user = await _userRepo.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException($"User {id} not found.");

        // Check username uniqueness (excluding current user)
        var existing = await _userRepo.GetByUsernameAsync(request.Username);
        if (existing is not null && existing.Id != id)
            throw new ArgumentException("Username already taken.");

        var existingEmail = await _userRepo.GetByEmailAsync(request.Email);
        if (existingEmail is not null && existingEmail.Id != id)
            throw new ArgumentException("Email already in use.");

        user.Username = request.Username;
        user.Email = request.Email;
        user.RoleName = request.Role;

        await _userRepo.UpdateAsync(user);
        return MapToDto(user);
    }

    public async Task DeleteAsync(int id)
    {
        var user = await _userRepo.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException($"User {id} not found.");
        await _userRepo.DeleteAsync(user.Id);
    }

    public async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(int userId, string keyName, int adminId)
    {
        _ = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await _apiKeyRepo.DeactivateAllForUserAsync(userId);

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey));
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var apiKey = new ApiKey
        {
            UserId = userId,
            KeyHash = keyHash,
            Name = keyName,
            IsActive = true,
            CreatedByAdminId = adminId
        };

        var id = await _apiKeyRepo.CreateAsync(apiKey);
        var created = await _apiKeyRepo.GetByIdAsync(id);

        return new ApiKeyCreatedResponse
        {
            Id = id,
            Name = keyName,
            Key = rawKey,
            CreatedAt = created?.CreatedAt ?? DateTime.UtcNow.ToString("o")
        };
    }

    public async Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(int userId)
    {
        _ = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var keys = await _apiKeyRepo.GetByUserIdAsync(userId);
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
        var key = await _apiKeyRepo.GetByIdAsync(keyId)
                  ?? throw new KeyNotFoundException($"API key {keyId} not found.");

        if (key.UserId != userId)
            throw new UnauthorizedAccessException("API key does not belong to this user.");

        await _apiKeyRepo.DeactivateAsync(keyId);
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        Role = u.RoleName,
        CreatedAt = u.CreatedAt
    };
}
