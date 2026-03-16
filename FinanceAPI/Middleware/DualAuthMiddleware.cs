using System.Security.Claims;
using System.Text;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using Microsoft.Extensions.Primitives;

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
            && context.Request.Query.TryGetValue("apiKey", out StringValues rawKey)
            && !string.IsNullOrEmpty(rawKey))
        {
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawKey!));
            string keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            ApiKey? apiKey = await apiKeyRepo.GetByHashAsync(keyHash);
            if (apiKey is not null)
            {
                User? user = await userRepo.GetByIdAsync(apiKey.UserId);
                if (user is not null)
                {
                    Claim[] claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.RoleName)
                    };
                    ClaimsIdentity identity = new ClaimsIdentity(claims, "ApiKey");
                    context.User = new ClaimsPrincipal(identity);
                }
            }
        }

        await _next(context);
    }
}
