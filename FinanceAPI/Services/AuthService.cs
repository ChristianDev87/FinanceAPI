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
    private readonly IUserRepository _userRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IConfiguration _config;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository userRepo, ICategoryRepository categoryRepo, IConfiguration config, IDbConnectionFactory connectionFactory, ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _categoryRepo = categoryRepo;
        _config = config;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Pre-compute the password hash outside the transaction: BCrypt is intentionally slow
        // and should not be recomputed on serialization-failure retries.
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        using IDbConnection conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
        }

        // All uniqueness checks + user count + insert run in a single Serializable transaction.
        // This ensures the "first user becomes Admin" rule and username/email uniqueness are
        // enforced atomically across multiple application instances.
        return await DbTransactionHelper.ExecuteInSerializableTransactionAsync(
            conn,
            txn => RegisterInTransactionAsync(request, passwordHash, conn, txn, cancellationToken),
            cancellationToken);
    }

    private async Task<AuthResponse> RegisterInTransactionAsync(
        RegisterRequest request,
        string passwordHash,
        IDbConnection conn,
        IDbTransaction txn,
        CancellationToken cancellationToken)
    {
        if (await _userRepo.GetByUsernameAsync(request.Username, conn, txn) is not null)
        {
            throw new ArgumentException("Username is already taken.");
        }

        if (await _userRepo.GetByEmailAsync(request.Email, conn, txn) is not null)
        {
            throw new ArgumentException("Email is already registered.");
        }

        bool hasUsers = await _userRepo.AnyAsync(conn, txn);
        string role = hasUsers ? UserRoles.User : UserRoles.Admin;

        User user = new User
        {
            Username = string.Concat(request.Username[0].ToString().ToUpper(), request.Username.AsSpan(1)),
            Email = request.Email,
            PasswordHash = passwordHash,
            RoleName = role
        };

        user.Id = await _userRepo.CreateAsync(user, conn, txn);

        List<DefaultCategoryConfig> defaultCategories =
            _config.GetSection("DefaultCategories").Get<List<DefaultCategoryConfig>>() ?? new();

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

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        User? user = await _userRepo.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Failed login attempt: unknown username '{Username}'.", request.Username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Failed login attempt: user {UserId} ({Username}) is deactivated.", user.Id, user.Username);
            throw new UnauthorizedAccessException("This account has been deactivated.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt: invalid password for user {UserId} ({Username}).", user.Id, user.Username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        _logger.LogInformation("User {UserId} ({Username}) logged in successfully.", user.Id, user.Username);

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
            new Claim("pwv", user.PasswordVersion.ToString(CultureInfo.InvariantCulture)),
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
