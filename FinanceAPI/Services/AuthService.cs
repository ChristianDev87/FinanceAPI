using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

    public AuthService(IUserRepository userRepo, ICategoryRepository categoryRepo, IConfiguration config)
    {
        _userRepo = userRepo;
        _categoryRepo = categoryRepo;
        _config = config;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepo.GetByUsernameAsync(request.Username) is not null)
            throw new ArgumentException("Username already taken.");

        if (await _userRepo.GetByEmailAsync(request.Email) is not null)
            throw new ArgumentException("Email already in use.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            RoleName = "User"
        };

        var userId = await _userRepo.CreateAsync(user);
        user.Id = userId;

        // Assign default categories
        var defaultCategories = _config.GetSection("DefaultCategories").Get<List<DefaultCategoryConfig>>() ?? new();
        for (var i = 0; i < defaultCategories.Count; i++)
        {
            var cat = defaultCategories[i];
            await _categoryRepo.CreateAsync(new Category
            {
                UserId = userId,
                Name = cat.Name,
                Color = cat.Color,
                Type = cat.Type,
                SortOrder = i
            });
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
        var user = await _userRepo.GetByUsernameAsync(request.Username)
                   ?? throw new KeyNotFoundException("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new KeyNotFoundException("Invalid username or password.");

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
        var jwtSettings = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.RoleName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(double.Parse(jwtSettings["ExpirationHours"]!)),
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
