using FinanceAPI.DTOs.Auth;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using FinanceAPI.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FinanceAPI.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var settings = new Dictionary<string, string?>
        {
            { "JwtSettings:SecretKey", "super-secret-key-for-testing-only-32-chars!!" },
            { "JwtSettings:Issuer",    "FinanceAPI-Test" },
            { "JwtSettings:Audience",  "FinanceAPI-Test" },
            { "JwtSettings:ExpirationHours", "1" },
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        _sut = new AuthService(_userRepo.Object, _categoryRepo.Object, config);

        // Default: at least one existing user so new registrations receive "User" role
        _userRepo.Setup(r => r.GetAllAsync())
                 .ReturnsAsync(new[] { new User { Id = 99, Username = "existing", RoleName = "Admin" } });
    }

    // ── Register ──────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_NewUser_ReturnsTokenAndUserInfo()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByEmailAsync("alice@test.com")).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync(1);

        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Email    = "alice@test.com",
            Password = "Password123!"
        });

        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.Equal("Alice", result.User.Username);
        Assert.Equal(1, result.User.Id);
    }

    [Fact]
    public async Task RegisterAsync_DefaultCategoriesCreated_CallsCategoryCreateForEachDefault()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync(42);

        // Config with 2 default categories
        var settings = new Dictionary<string, string?>
        {
            { "JwtSettings:SecretKey",       "super-secret-key-for-testing-only-32-chars!!" },
            { "JwtSettings:Issuer",           "FinanceAPI-Test" },
            { "JwtSettings:Audience",         "FinanceAPI-Test" },
            { "JwtSettings:ExpirationHours",  "1" },
            { "DefaultCategories:0:Name",     "Gehalt" },
            { "DefaultCategories:0:Type",     "income" },
            { "DefaultCategories:0:Color",    "#1cc88a" },
            { "DefaultCategories:1:Name",     "Lebensmittel" },
            { "DefaultCategories:1:Type",     "expense" },
            { "DefaultCategories:1:Color",    "#e74a3b" },
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var sut = new AuthService(_userRepo.Object, _categoryRepo.Object, config);

        await sut.RegisterAsync(new RegisterRequest { Username = "bob", Email = "bob@test.com", Password = "pass" });

        _categoryRepo.Verify(r => r.CreateAsync(It.IsAny<FinanceAPI.Models.Category>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsername_ThrowsArgumentException()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("alice"))
                 .ReturnsAsync(new User { Id = 1, Username = "alice" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.RegisterAsync(new RegisterRequest
            {
                Username = "alice",
                Email    = "other@test.com",
                Password = "Password123!"
            }));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsArgumentException()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByEmailAsync("alice@test.com"))
                 .ReturnsAsync(new User { Id = 2, Email = "alice@test.com" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.RegisterAsync(new RegisterRequest
            {
                Username = "alice",
                Email    = "alice@test.com",
                Password = "Password123!"
            }));
    }

    // ── Login ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4);
        _userRepo.Setup(r => r.GetByUsernameAsync("alice"))
                 .ReturnsAsync(new User
                 {
                     Id = 1, Username = "alice", Email = "alice@test.com",
                     PasswordHash = hash, RoleName = "User", IsActive = true
                 });

        var result = await _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "Password123!" });

        Assert.NotNull(result.Token);
        Assert.Equal("alice", result.User.Username);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("ghost")).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.LoginAsync(new LoginRequest { Username = "ghost", Password = "pass" }));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsKeyNotFoundException()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("correct", workFactor: 4);
        _userRepo.Setup(r => r.GetByUsernameAsync("alice"))
                 .ReturnsAsync(new User { Id = 1, Username = "alice", PasswordHash = hash, IsActive = true });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "wrong" }));
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ThrowsUnauthorizedAccessException()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4);
        _userRepo.Setup(r => r.GetByUsernameAsync("alice"))
                 .ReturnsAsync(new User
                 {
                     Id = 1, Username = "alice", PasswordHash = hash, IsActive = false
                 });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "Password123!" }));
    }
}
