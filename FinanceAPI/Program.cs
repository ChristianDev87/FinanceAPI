using System.Security.Claims;
using System.Text;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Middleware;
using FinanceAPI.Models;
using FinanceAPI.Repositories;
using FinanceAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Kestrel: nur HTTP wenn DISABLE_HTTPS_REDIRECT gesetzt (z.B. Docker ohne Zertifikat)
if (Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT") == "true")
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5281);
    });
}



// ── Controllers ────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger ─────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FinanceAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API key authentication via the X-Api-Key request header.",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Database ────────────────────────────────────────────────────
string provider = builder.Configuration["DatabaseSettings:Provider"] ?? "sqlite";

IDbConnectionFactory dbFactory;
ISqlDialect dialect;

switch (provider.ToLowerInvariant())
{
    case "postgresql":
    case "postgres":
        dbFactory = new PostgreSqlConnectionFactory(
            builder.Configuration.GetConnectionString("DefaultConnection")!);
        dialect = new PostgreSqlDialect();
        break;

    case "mysql":
        dbFactory = new MySqlConnectionFactory(
            builder.Configuration.GetConnectionString("DefaultConnection")!);
        dialect = new MySqlDialect();
        break;

    default: // sqlite
        // Resolve relative paths against ContentRootPath so the location is
        // consistent regardless of working directory (dev, published, Docker, …)
        string? rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection");

        // In test environments the factory replaces IDbConnectionFactory, so a missing
        // connection string is expected. In all other environments it is a fatal misconfiguration.
        if (rawConnStr is null && !builder.Environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. " +
                "Copy appsettings.example.json to appsettings.json and set a valid connection string.");
        }

        string connectionString = rawConnStr ?? string.Empty;

        if (rawConnStr is not null)
        {
            string? dataSource = rawConnStr.Split(';')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("Data Source=".Length);

            if (dataSource is not null && !Path.IsPathRooted(dataSource))
            {
                string fullPath = Path.GetFullPath(dataSource, builder.Environment.ContentRootPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                connectionString = $"Data Source={fullPath}";
            }
        }

        dbFactory = new SqliteConnectionFactory(connectionString);
        dialect = new SqliteDialect();
        break;
}

builder.Services.AddSingleton<IDbConnectionFactory>(_ => dbFactory);
builder.Services.AddSingleton<ISqlDialect>(_ => dialect);
builder.Services.AddSingleton<DatabaseInitializer>();

// ── Repositories ────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// ── Services ────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

// ── JWT Auth ────────────────────────────────────────────────────
// Fast startup validation — skipped in Testing because the factory supplies the key
// via IConfiguration after Program.cs has already run.
if (!builder.Environment.IsEnvironment("Testing"))
{
    string startupKey = builder.Configuration["JwtSettings:SecretKey"]
        ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
    if (startupKey.Length < 32)
    {
        throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters long.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Resolve TokenValidationParameters from the DI IConfiguration so that
// WebApplicationFactory config overrides are applied before the key is read.
// This keeps the validation key in sync with AuthService.GenerateToken().
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, config) =>
    {
        IConfigurationSection jwt = config.GetSection("JwtSettings");
        string key = jwt["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
        if (key.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters long.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)) { KeyId = "finance-api-key" }
        };

        // Re-validate user status on every authenticated request so that
        // deactivated or deleted accounts are rejected immediately, without
        // waiting for the JWT to expire naturally.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                string? userIdStr = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out int userId))
                {
                    context.Fail("Invalid user identifier claim.");
                    return;
                }

                IUserRepository userRepo = context.HttpContext.RequestServices
                    .GetRequiredService<IUserRepository>();

                User? user = await userRepo.GetByIdAsync(userId);
                if (user is null || !user.IsActive)
                {
                    context.Fail("User account is disabled or does not exist.");
                    return;
                }

                // If the role stored in the token no longer matches the DB, rebuild the
                // ClaimsPrincipal so that role changes take effect immediately without
                // requiring a new login.
                ClaimsPrincipal currentPrincipal = context.Principal!;
                string? tokenRole = currentPrincipal.FindFirstValue(ClaimTypes.Role);
                if (!string.Equals(tokenRole, user.RoleName, StringComparison.Ordinal))
                {
                    List<Claim> updatedClaims = currentPrincipal.Claims
                        .Where(c => c.Type != ClaimTypes.Role)
                        .Append(new Claim(ClaimTypes.Role, user.RoleName))
                        .ToList();
                    ClaimsIdentity identity = new ClaimsIdentity(updatedClaims, "Bearer");
                    context.Principal = new ClaimsPrincipal(identity);
                }
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", config =>
    {
        // Disable rate limiting in the test environment to prevent concurrent
        // integration tests from hitting the 429 limit.
        config.PermitLimit = builder.Environment.IsEnvironment("Testing") ? int.MaxValue : 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ─────────────────────────────────────────────────────────
string[] allowedOrigins = builder.Configuration
    .GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// ── Health checks ────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ═════════════════════════════════════════════════════════════════
WebApplication app = builder.Build();

// ── DB Init ──────────────────────────────────────────────────────
using (IServiceScope scope = app.Services.CreateScope())
{
    DatabaseInitializer dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await dbInit.InitializeAsync();
}

// ── Middleware Pipeline ──────────────────────────────────────────

// Attach a correlation ID to every response so clients and logs can be correlated.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
        {
            context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
        }
        return Task.CompletedTask;
    });
    await next(context);
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseRateLimiter();

//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinanceAPI v1"));
//}

if (Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT") != "true")
{
    app.UseHttpsRedirection();
}
app.UseCors();

app.UseMiddleware<DualAuthMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
