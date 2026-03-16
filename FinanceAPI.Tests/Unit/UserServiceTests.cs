using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using FinanceAPI.Services;
using Moq;

namespace FinanceAPI.Tests.Unit;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IApiKeyRepository> _apiKeyRepo = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_userRepo.Object, _apiKeyRepo.Object);
    }

    private static User MakeUser(int id, string username = "alice") => new()
    {
        Id = id,
        Username = username,
        Email = $"{username}@test.com",
        PasswordHash = "hash",
        RoleName = "User",
        IsActive = true,
        CreatedAt = "2026-01-01 00:00:00"
    };

    // ── GetAll ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        _userRepo.Setup(r => r.GetAllAsync())
                 .ReturnsAsync(new[] { MakeUser(1), MakeUser(2, "bob") });

        List<UserDto> result = (await _sut.GetAllAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("alice", result[0].Username);
        Assert.Equal("bob", result[1].Username);
    }

    // ── GetById ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsDto()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));

        UserDto result = await _sut.GetByIdAsync(1);

        Assert.Equal(1, result.Id);
        Assert.Equal("alice", result.Username);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetByIdAsync(99));
    }

    // ── Update ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReturnsUpdatedDto()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync(MakeUser(1));   // same user
        _userRepo.Setup(r => r.GetByEmailAsync("alice@test.com")).ReturnsAsync(MakeUser(1));

        UserDto result = await _sut.UpdateAsync(1, new UpdateUserRequest
        {
            Username = "alice",
            Email = "alice@test.com",
            Role = "User"
        });

        Assert.Equal("alice", result.Username);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.UpdateAsync(99, new UpdateUserRequest { Username = "x", Email = "x@x.com", Role = "User" }));
    }

    [Fact]
    public async Task UpdateAsync_UsernameTakenByOtherUser_ThrowsArgumentException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));
        _userRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(MakeUser(2, "bob"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpdateAsync(1, new UpdateUserRequest
            {
                Username = "bob",
                Email = "alice@test.com",
                Role = "User"
            }));
    }

    [Fact]
    public async Task UpdateAsync_EmailTakenByOtherUser_ThrowsArgumentException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync(MakeUser(1));
        _userRepo.Setup(r => r.GetByEmailAsync("bob@test.com")).ReturnsAsync(MakeUser(2, "bob"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpdateAsync(1, new UpdateUserRequest
            {
                Username = "alice",
                Email = "bob@test.com",
                Role = "User"
            }));
    }

    // ── Delete ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingUser_Deletes()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));

        await _sut.DeleteAsync(1);

        _userRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.DeleteAsync(99));
    }

    // ── SetActive ─────────────────────────────────────────────────

    [Fact]
    public async Task SetActiveAsync_ExistingUser_CallsRepository()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));

        await _sut.SetActiveAsync(1, false);

        _userRepo.Verify(r => r.SetActiveAsync(1, false), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.SetActiveAsync(99, false));
    }

    // ── ChangePassword ────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_UpdatesHash()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("OldPass1", workFactor: 4);
        _userRepo.Setup(r => r.GetByIdAsync(1))
                 .ReturnsAsync(new User { Id = 1, Username = "alice", PasswordHash = hash, RoleName = "User", IsActive = true });

        await _sut.ChangePasswordAsync(1, "OldPass1", "NewPass1");

        _userRepo.Verify(r => r.UpdatePasswordAsync(1, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsUnauthorizedAccessException()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("OldPass1", workFactor: 4);
        _userRepo.Setup(r => r.GetByIdAsync(1))
                 .ReturnsAsync(new User { Id = 1, Username = "alice", PasswordHash = hash, RoleName = "User", IsActive = true });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ChangePasswordAsync(1, "WrongPass", "NewPass1"));
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ChangePasswordAsync(99, "any", "NewPass1"));
    }

    // ── AdminSetPassword ──────────────────────────────────────────

    [Fact]
    public async Task AdminSetPasswordAsync_ExistingUser_UpdatesHash()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));

        await _sut.AdminSetPasswordAsync(1, "NewPass1");

        _userRepo.Verify(r => r.UpdatePasswordAsync(1, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AdminSetPasswordAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.AdminSetPasswordAsync(99, "NewPass1"));
    }

    // ── API Keys ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateApiKeyAsync_ExistingUser_ReturnsKeyWithPlaintext()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));
        _apiKeyRepo.Setup(r => r.CreateAsync(It.IsAny<ApiKey>())).ReturnsAsync(10);
        _apiKeyRepo.Setup(r => r.GetByIdAsync(10))
                   .ReturnsAsync(new ApiKey { Id = 10, UserId = 1, Name = "CI Key", IsActive = true, CreatedAt = "2026-01-01 00:00:00" });

        ApiKeyCreatedResponse result = await _sut.CreateApiKeyAsync(1, "CI Key");

        Assert.Equal(10, result.Id);
        Assert.Equal("CI Key", result.Name);
        Assert.NotEmpty(result.Key);
        _apiKeyRepo.Verify(r => r.DeactivateAllForUserAsync(1), Times.Once);
    }

    [Fact]
    public async Task CreateApiKeyAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateApiKeyAsync(99, "key"));
    }

    [Fact]
    public async Task GetApiKeysAsync_ExistingUser_ReturnsMappedDtos()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1));
        _apiKeyRepo.Setup(r => r.GetByUserIdAsync(1))
                   .ReturnsAsync(new[]
                   {
                       new ApiKey { Id = 1, UserId = 1, Name = "Key1", IsActive = true, CreatedAt = "2026-01-01 00:00:00" },
                       new ApiKey { Id = 2, UserId = 1, Name = "Key2", IsActive = false, CreatedAt = "2026-01-02 00:00:00" },
                   });

        List<ApiKeyDto> result = (await _sut.GetApiKeysAsync(1)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Key1", result[0].Name);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_OwnKey_CallsDeactivate()
    {
        _apiKeyRepo.Setup(r => r.GetByIdAsync(1))
                   .ReturnsAsync(new ApiKey { Id = 1, UserId = 1, Name = "Test" });

        await _sut.RevokeApiKeyAsync(1, 1);

        _apiKeyRepo.Verify(r => r.DeactivateAsync(1), Times.Once);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_KeyNotFound_ThrowsKeyNotFoundException()
    {
        _apiKeyRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((ApiKey?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.RevokeApiKeyAsync(1, 99));
    }

    [Fact]
    public async Task RevokeApiKeyAsync_KeyBelongsToOtherUser_ThrowsUnauthorizedAccessException()
    {
        _apiKeyRepo.Setup(r => r.GetByIdAsync(1))
                   .ReturnsAsync(new ApiKey { Id = 1, UserId = 99, Name = "Other" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RevokeApiKeyAsync(1, 1));
    }
}
