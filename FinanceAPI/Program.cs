using System.Text;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Middleware;
using FinanceAPI.Repositories;
using FinanceAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;

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
IConfigurationSection jwtSettings = builder.Configuration.GetSection("JwtSettings");

string jwtSecretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

if (jwtSecretKey.Length < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters long.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 10;
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

// ═════════════════════════════════════════════════════════════════
WebApplication app = builder.Build();

// ── DB Init ──────────────────────────────────────────────────────
using (IServiceScope scope = app.Services.CreateScope())
{
    DatabaseInitializer dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await dbInit.InitializeAsync();
}

// ── Middleware Pipeline ──────────────────────────────────────────
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

app.Run();

public partial class Program { }
