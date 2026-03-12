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

var builder = WebApplication.CreateBuilder(args);

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
var provider = builder.Configuration["DatabaseSettings:Provider"] ?? "sqlite";

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
        var rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection");

        // In test environments the factory replaces IDbConnectionFactory, so a missing
        // connection string is expected. In all other environments it is a fatal misconfiguration.
        if (rawConnStr is null && !builder.Environment.IsEnvironment("Testing"))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. " +
                "Copy appsettings.example.json to appsettings.json and set a valid connection string.");

        var connectionString = rawConnStr ?? string.Empty;

        if (rawConnStr is not null)
        {
            var dataSource = rawConnStr.Split(';')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("Data Source=".Length);

            if (dataSource is not null && !Path.IsPathRooted(dataSource))
            {
                var fullPath = Path.GetFullPath(dataSource, builder.Environment.ContentRootPath);
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
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
        };
    });

builder.Services.AddAuthorization();

// ── CORS ─────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
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
var app = builder.Build();

// ── DB Init ──────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await dbInit.InitializeAsync();
}

// ── Middleware Pipeline ──────────────────────────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinanceAPI v1"));
}

app.UseHttpsRedirection();
app.UseCors();

app.UseMiddleware<DualAuthMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
