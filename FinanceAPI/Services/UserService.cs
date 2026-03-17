using System.Data;
using System.Security.Cryptography;
using FinanceAPI.Database;
using FinanceAPI.Domain;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Exceptions;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;

namespace FinanceAPI.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepo, IApiKeyRepository apiKeyRepo, IDbConnectionFactory connectionFactory, ILogger<UserService> logger)
    {
        _userRepo = userRepo;
        _apiKeyRepo = apiKeyRepo;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<User> users = await _userRepo.GetAllAsync(cancellationToken);
        return users.Select(MapToDto);
    }

    public async Task<UserDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        User user = await _userRepo.GetByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException($"User {id} not found.");
        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, bool allowRoleChange = false, CancellationToken cancellationToken = default)
    {
        User user = await _userRepo.GetByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException($"User {id} not found.");

        User? existing = await _userRepo.GetByUsernameAsync(request.Username, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            throw new ArgumentException("Username already taken.");
        }

        User? existingEmail = await _userRepo.GetByEmailAsync(request.Email, cancellationToken);
        if (existingEmail is not null && existingEmail.Id != id)
        {
            throw new ArgumentException("Email already in use.");
        }

        // Demoting an active admin requires an atomic check that another active admin remains.
        // The Serializable transaction prevents a concurrent request from racing past this guard.
        if (allowRoleChange && user.RoleName == UserRoles.Admin && user.IsActive && request.Role != UserRoles.Admin)
        {
            using IDbConnection conn = _connectionFactory.CreateConnection();
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            await DbTransactionHelper.ExecuteInSerializableTransactionAsync(conn, async txn =>
            {
                int activeAdmins = await _userRepo.CountActiveAdminsAsync(conn, txn);
                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException("Cannot demote the last active admin.");
                }

                string oldRole = user.RoleName;
                user.Username = request.Username;
                user.Email = request.Email;
                user.RoleName = request.Role;
                await _userRepo.UpdateAsync(user, conn, txn);
                _logger.LogInformation("User {UserId} role changed from {OldRole} to {NewRole}.", id, oldRole, request.Role);
            }, cancellationToken);

            return MapToDto(user);
        }

        string previousRole = user.RoleName;
        user.Username = request.Username;
        user.Email = request.Email;
        user.RoleName = allowRoleChange ? request.Role : user.RoleName;

        await _userRepo.UpdateAsync(user, cancellationToken);

        if (allowRoleChange && previousRole != user.RoleName)
        {
            _logger.LogInformation("User {UserId} role changed from {OldRole} to {NewRole}.", id, previousRole, user.RoleName);
        }

        return MapToDto(user);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        User user = await _userRepo.GetByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException($"User {id} not found.");

        if (user.RoleName == UserRoles.Admin && user.IsActive)
        {
            using IDbConnection conn = _connectionFactory.CreateConnection();
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            await DbTransactionHelper.ExecuteInSerializableTransactionAsync(conn, async txn =>
            {
                int activeAdmins = await _userRepo.CountActiveAdminsAsync(conn, txn);
                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last active admin.");
                }

                await _userRepo.DeleteAsync(user.Id, conn, txn);
            }, cancellationToken);

            _logger.LogInformation("User {UserId} ({Username}) deleted.", id, user.Username);
            return;
        }

        await _userRepo.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("User {UserId} ({Username}) deleted.", id, user.Username);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        User user = await _userRepo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"User {id} not found.");

        if (!isActive && user.RoleName == UserRoles.Admin && user.IsActive)
        {
            using IDbConnection conn = _connectionFactory.CreateConnection();
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            await DbTransactionHelper.ExecuteInSerializableTransactionAsync(conn, async txn =>
            {
                int activeAdmins = await _userRepo.CountActiveAdminsAsync(conn, txn);
                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException("Cannot deactivate the last active admin.");
                }

                await _userRepo.SetActiveAsync(id, isActive, conn, txn);
            }, cancellationToken);

            _logger.LogInformation("User {UserId} ({Username}) set active={IsActive}.", id, user.Username, isActive);
            return;
        }

        await _userRepo.SetActiveAsync(id, isActive, cancellationToken);
        _logger.LogInformation("User {UserId} ({Username}) set active={IsActive}.", id, user.Username, isActive);
    }

    public async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(int userId, string keyName, int? createdByAdminId = null, CancellationToken cancellationToken = default)
    {
        _ = await _userRepo.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} not found.");

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

        ApiKey created = await _apiKeyRepo.GetByIdAsync(newKeyId, cancellationToken)
                        ?? throw new InvalidOperationException($"Failed to retrieve newly created API key {newKeyId}.");

        _logger.LogInformation("API key {KeyId} ({KeyName}) created for user {UserId}.", newKeyId, keyName, userId);

        return new ApiKeyCreatedResponse
        {
            Id = newKeyId,
            Name = keyName,
            Key = rawKey,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync(int userId, CancellationToken cancellationToken = default)
    {
        _ = await _userRepo.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} not found.");

        IEnumerable<ApiKey> keys = await _apiKeyRepo.GetByUserIdAsync(userId, cancellationToken);
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

    public async Task RevokeApiKeyAsync(int userId, int keyId, CancellationToken cancellationToken = default)
    {
        ApiKey key = await _apiKeyRepo.GetByIdAsync(keyId, cancellationToken)
                  ?? throw new NotFoundException($"API key {keyId} not found.");

        if (key.UserId != userId)
        {
            throw new ForbiddenException("API key does not belong to this user.");
        }

        await _apiKeyRepo.DeactivateAsync(keyId, cancellationToken);
        _logger.LogInformation("API key {KeyId} revoked for user {UserId}.", keyId, userId);
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        User user = await _userRepo.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User {userId} not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        await _userRepo.UpdatePasswordAsync(userId, BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12), cancellationToken);
    }

    public async Task AdminSetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        _ = await _userRepo.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} not found.");

        await _userRepo.UpdatePasswordAsync(userId, BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12), cancellationToken);
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
