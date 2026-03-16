using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceAPI.Database;
using FinanceAPI.Domain;
using FinanceAPI.DTOs.Auth;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace FinanceAPI.Services;

public class AuthService : IAuthService
{
    // Serializes concurrent registrations to prevent two simultaneous requests
    // from both seeing 0 users and both receiving the Admin role.
    // Note: single-instance only — a distributed lock would be required for multi-node deployments.
    private static readonly SemaphoreSlim _registerLock = new(1, 1);

    private readonly IUserRepository _userRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IConfiguration _config;
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthService(IUserRepository userRepo, ICategoryRepository categoryRepo, IConfiguration config, IDbConnectionFactory connectionFactory)
    {
        _userRepo = userRepo;
        _categoryRepo = categoryRepo;
        _config = config;
        _connectionFactory = connectionFactory;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        await _registerLock.WaitAsync();
        try
        {
            return await RegisterInternalAsync(request);
        }
        finally
        {
            _registerLock.Release();
        }
    }

    private async Task<AuthResponse> RegisterInternalAsync(RegisterRequest request)
    {
        string role = UserRoles.User;

        if (await _userRepo.GetByUsernameAsync(request.Username) is not null)
        {
            throw new ArgumentException("Username is already taken.");
        }

        if (await _userRepo.GetByEmailAsync(request.Email) is not null)
        {
            throw new ArgumentException("Email is already registered.");
        }

        if (!await _userRepo.AnyAsync())
        {
            role = UserRoles.Admin;
        }

        User user = new User
        {
            Username = string.Concat(request.Username[0].ToString().ToUpper(), request.Username.AsSpan(1)), //First letter uppercase for username
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            RoleName = role
        };

        List<DefaultCategoryConfig> defaultCategories = _config.GetSection("DefaultCategories").Get<List<DefaultCategoryConfig>>() ?? new();

        using IDbConnection conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
        }

        using IDbTransaction txn = conn.BeginTransaction();
        try
        {
            user.Id = await _userRepo.CreateAsync(user, conn, txn);

            for (int i = 0; i < defaultCategories.Count; i++)
            {
                DefaultCategoryConfig cat = defaultCategories[i];
                await _categoryRepo.CreateAsync(new Category
                {
                    UserId = user.Id,
                    Name = cat.Name,
                    Color = cat.Color,
                    Type = cat.Type,
                    SortOrder = i
                }, conn, txn);
            }

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
            throw;
        }

        return new AuthResponse
        {
            Token = GenerateToken(user),
            User = new AuthResponse.UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.RoleName
            }
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        User user = await _userRepo.GetByUsernameAsync(request.Username)
                   ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account has been deactivated.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        return new AuthResponse
        {
            Token = GenerateToken(user),
            User = new AuthResponse.UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.RoleName
            }
        };
    }

    public string GenerateToken(User user)
    {
        IConfigurationSection jwtSettings = _config.GetSection("JwtSettings");
        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)) { KeyId = "finance-api-key" };
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.RoleName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        JwtSecurityToken token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(double.Parse(jwtSettings["ExpirationHours"]!, CultureInfo.InvariantCulture)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private class DefaultCategoryConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Color { get; set; } = "#1abc9c";
    }
}
