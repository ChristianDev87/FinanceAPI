using System.Security.Claims;
using System.Text;
using FinanceAPI.Interfaces.Repositories;

namespace FinanceAPI.Middleware;

public class DualAuthMiddleware
{
    private readonly RequestDelegate _next;

    public DualAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyRepository apiKeyRepo, IUserRepository userRepo)
    {
        // Only attempt API key auth if no Authorization header is present
        if (!context.Request.Headers.ContainsKey("Authorization")
            && context.Request.Query.TryGetValue("apiKey", out var rawKey)
            && !string.IsNullOrEmpty(rawKey))
        {
            var hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawKey!));
            var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var apiKey = await apiKeyRepo.GetByHashAsync(keyHash);
            if (apiKey is not null)
            {
                var user = await userRepo.GetByIdAsync(apiKey.UserId);
                if (user is not null)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.RoleName)
                    };
                    var identity = new ClaimsIdentity(claims, "ApiKey");
                    context.User = new ClaimsPrincipal(identity);
                }
            }
        }

        await _next(context);
    }
}
