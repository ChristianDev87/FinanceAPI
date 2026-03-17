# FinanceAPI

A multi-user personal finance REST API built with .NET 10, Dapper and SQLite, PostgreSQL or MySQL.

## Features

- JWT Bearer + API Key dual authentication
- Role-based authorization (`Admin` / `User`)
- Full CRUD for categories and transactions
- Monthly and category-based statistics
- Self-service profile management (username, email, password, account deletion)
- Self-service API key management per user
- Admin panel: user management, role assignment, password reset, account locking
- Automatic schema migration on startup
- Swagger UI for interactive testing

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 10, ASP.NET Core Web API |
| Data access | Dapper + SQLite / PostgreSQL / MySQL |
| Password hashing | BCrypt.Net-Next (work factor 12) |
| Authentication | JWT Bearer + SHA-256 API keys |
| API docs | Swashbuckle / OpenAPI |

## Deployment Model

The following invariants are enforced atomically via **Serializable database transactions** with retry logic:

- The first registered user is automatically assigned the `Admin` role.
- At least one active `Admin` must exist at all times (demotion, deletion, and deactivation are all guarded).
- At most one active API key exists per user at any time (enforced by a Serializable transaction on key creation; SQLite and PostgreSQL additionally enforce this at the schema level via a partial unique index).

These checks are safe under **multi-instance deployments** (e.g. Kubernetes, multiple Docker containers behind a load balancer). Concurrent requests on different replicas are serialized by the database and retried automatically on transient conflicts (serialization failures and deadlocks).

> For SQLite deployments, the Serializable isolation level maps to `BEGIN EXCLUSIVE`, so multi-writer concurrency is limited by SQLite's file-level locking. For production high-concurrency workloads, PostgreSQL or MySQL is recommended.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

```bash
# 1. Clone the repository
git clone https://github.com/ChristianDev87/FinanceAPI.git
cd FinanceAPI/FinanceAPI

# 2. Run the API
dotnet run
```

The SQLite database (`finance.db`) and all tables are created automatically on first start. Schema migrations (e.g. new columns) are applied on every startup and are idempotent.

### Swagger UI

```
https://localhost:7185/swagger
```

## Configuration (`appsettings.json`)

| Key | Description |
|-----|-------------|
| `Kestrel.Endpoints.Http.Url` | HTTP listen address (default `http://localhost:5281`) |
| `Kestrel.Endpoints.Https.Url` | HTTPS listen address (default `https://localhost:7185`) |
| `DatabaseSettings.Provider` | Database provider: `sqlite` (default), `postgresql`, `mysql` |
| `ConnectionStrings.DefaultConnection` | Connection string for the selected provider |
| `JwtSettings.SecretKey` | **Required.** At least 32 characters. Never commit to source control |
| `JwtSettings.Issuer` | JWT issuer claim |
| `JwtSettings.Audience` | JWT audience claim |
| `JwtSettings.ExpirationHours` | Token lifetime in hours (default `24`) |
| `ForwardedHeadersSettings.Enabled` | Set to `true` to trust `X-Forwarded-For` from listed proxies (default: `false`) |
| `ForwardedHeadersSettings.TrustedProxies` | Array of trusted proxy IPs (e.g. `["10.0.0.1"]`). Only used when `Enabled: true` |
| `ForwardedHeadersSettings.ForwardLimit` | Number of proxy hops to process from `X-Forwarded-For` (default: `1`). Set to `0` for unlimited (multi-proxy chains). Must match the actual number of trusted proxies in front of the API. |
| `RateLimitSettings.AuthPermitLimit` | Max auth requests per IP per window (default `10`) |
| `RateLimitSettings.AuthWindowMinutes` | Rate-limit window in minutes (default `1`) |
| `CorsSettings.AllowedOrigins` | Allowed frontend origins in production |
| `DefaultCategories` | Category list auto-assigned to every new user on registration |
| `SwaggerSettings.Enabled` | Set to `true` to enable Swagger UI in non-Development environments (default: `false`) |

> In **Development** mode CORS allows any origin. In **Production** only the origins listed in `CorsSettings.AllowedOrigins` are allowed.

## API Reference

All protected routes require either `Authorization: Bearer <jwt>` or `X-Api-Key: <key>`.

### Auth

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/register` | – | Register new account, returns JWT + user info |
| POST | `/api/auth/login` | – | Login, returns JWT + user info |

### Profile *(authenticated user)*

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/profile` | Get own profile |
| PUT | `/api/profile` | Update own username and email |
| PUT | `/api/profile/password` | Change own password (requires current password) |
| DELETE | `/api/profile` | Delete own account and all associated data |
| GET | `/api/profile/apikeys` | List own API keys |
| POST | `/api/profile/apikeys` | Create a new API key (deactivates existing ones) |
| DELETE | `/api/profile/apikeys/{keyId}` | Revoke an API key |

### Users *(Admin only)*

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/users` | List all users |
| GET | `/api/users/{id}` | Get user by ID |
| PUT | `/api/users/{id}` | Update username, email and role |
| PUT | `/api/users/{id}/password` | Set a new password for a user (no current password required) |
| PUT | `/api/users/{id}/active` | Lock (`false`) or unlock (`true`) a user account |
| DELETE | `/api/users/{id}` | Delete user and all their data |
| GET | `/api/users/{id}/apikeys` | List API keys of a user |
| POST | `/api/users/{id}/apikeys` | Generate an API key for a user |
| DELETE | `/api/users/{id}/apikeys/{keyId}` | Revoke an API key |

### Categories *(authenticated user)*

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/categories` | List own categories |
| POST | `/api/categories` | Create category |
| PUT | `/api/categories/{id}` | Update category (name, color, type) |
| DELETE | `/api/categories/{id}` | Delete category (blocked if transactions exist) |
| PUT | `/api/categories/reorder` | Update sort order (array of `{id, sortOrder}`) |

### Transactions *(authenticated user)*

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/transactions` | List transactions — optional filters: `month`, `year`, `categoryId`, `type` |
| GET | `/api/transactions/{id}` | Get transaction by ID |
| POST | `/api/transactions` | Create transaction |
| PUT | `/api/transactions/{id}` | Update transaction |
| DELETE | `/api/transactions/{id}` | Delete transaction |

### Statistics *(authenticated user)*

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/statistics/years` | List of years that have transactions |
| GET | `/api/statistics/monthly?year={y}` | Income and expense totals per month (`year` optional, defaults to current year) |
| GET | `/api/statistics/categories?month={m}&year={y}&type={t}` | Totals grouped by category (all params optional, default to current month/year) |

### Health

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/health` | – | Returns `Healthy` when the application is running |

## Authentication

### JWT (Browser / Frontend)

After a successful login or registration the response contains a `token`. Include it in every subsequent request:

```
Authorization: Bearer <token>
```

### API Key (Scripts / Integrations)

API keys are generated via `POST /api/profile/apikeys` (self-service) or `POST /api/users/{id}/apikeys` (admin). The **plaintext key is returned only once** — store it securely immediately.

Pass the key as a request header on any protected endpoint:

```
GET /api/transactions
X-Api-Key: <key>
```

Only the SHA-256 hash of the key is stored in the database. Creating a new key automatically deactivates all previous keys for that user.

### Auth Priority

When both `Authorization: Bearer` and `X-Api-Key` are present on the same request, the **JWT takes priority** and the API key header is ignored. API key authentication is only attempted when no `Authorization` header is present.

## Roles

| Role | How assigned | Access |
|------|-------------|--------|
| `User` | Default on registration | Own profile, categories, transactions, statistics, API keys |
| `Admin` | Automatically assigned to the first registered user; afterwards assignable by an existing Admin via `PUT /api/users/{id}` | Everything above + full user management |

> Role and lock-status changes take effect immediately for both API key auth and JWT.
> On every authenticated request the user's current role and active status are verified against the database. A locked account is rejected with 401 even if a valid JWT is still present. A role change (e.g. Admin → User) is reflected immediately without requiring a new login. A password change or admin password reset also invalidates all existing JWTs for that user immediately — the next request with an old token returns 401.

## Project Structure

```
FinanceAPI/
├── Controllers/        AuthController, ProfileController, UsersController,
│                       CategoriesController, TransactionsController, StatisticsController
├── Database/           IDbConnectionFactory, ISqlDialect, DbTransactionHelper,
│                       SqliteConnectionFactory, PostgreSqlConnectionFactory, MySqlConnectionFactory,
│                       SqliteDialect, PostgreSqlDialect, MySqlDialect,
│                       DatabaseInitializer, DateOnlyTypeHandler,
│                       Migrations/{SQLite,PostgreSQL,MySQL}/V001__initial_schema.sql,
│                       Migrations/{SQLite,PostgreSQL,MySQL}/V002__password_version_apikey_constraint.sql
├── Domain/             UserRoles (Admin, User), TransactionTypes (income, expense)
├── DTOs/               Auth/, Users/, ApiKeys/, Categories/,
│                       Transactions/, Statistics/, Profile/
├── Exceptions/         ConflictException, NotFoundException, ForbiddenException
├── Interfaces/
│   ├── Repositories/   IUserRepository, IApiKeyRepository,
│   │                   ICategoryRepository, ITransactionRepository
│   └── Services/       IAuthService, IUserService, ICategoryService,
│                       ITransactionService, IStatisticsService
├── Middleware/         ErrorHandlingMiddleware (global error → JSON)
│                       DualAuthMiddleware (JWT + API key)
├── Models/             User, ApiKey, Category, Transaction, Role
├── Repositories/       Dapper implementations
├── Services/           Business logic (with structured audit logging)
└── Program.cs          DI registration, middleware pipeline
```

## Error Handling

All errors are returned as JSON by `ErrorHandlingMiddleware`:

```json
{ "error": "Human-readable message", "statusCode": 404 }
```

| Exception type | HTTP status |
|----------------|-------------|
| `NotFoundException` | 404 Not Found |
| `ForbiddenException` | 403 Forbidden |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `ConflictException` | 409 Conflict |
| `ArgumentException` / `InvalidOperationException` | 400 Bad Request |
| DB unique constraint violation | 409 Conflict |
| Any other | 500 Internal Server Error (reference ID logged) |

Every response includes an `X-Correlation-Id` header containing the ASP.NET Core trace identifier. Use it to correlate client errors with server logs.

500 responses contain a short reference ID (`Reference: abc12345`) that can be found in the server log alongside the full exception.

## Database

The database provider is selected via `DatabaseSettings:Provider` in `appsettings.json`. Schema migrations are applied automatically on startup via versioned SQL files in `Database/Migrations/{Provider}/`. The `SchemaVersions` table tracks which migrations have been applied; only new migrations run on each startup.

| Provider value | Database | Migrations folder |
|---|---|---|
| `sqlite` (default) | SQLite | `Database/Migrations/SQLite/` |
| `postgresql` / `postgres` | PostgreSQL 13+ | `Database/Migrations/PostgreSQL/` |
| `mysql` | MySQL 8.0.13+ / MariaDB 10.6+ | `Database/Migrations/MySQL/` |

### SQLite (default)

```json
"DatabaseSettings": { "Provider": "sqlite" },
"ConnectionStrings": { "DefaultConnection": "Data Source=data/finance.db" }
```

The database file is created automatically in the `data/` subfolder relative to the application root. No external server required. Foreign key enforcement (`ON DELETE CASCADE` / `ON DELETE SET NULL`) is activated automatically on every connection.

### PostgreSQL

```json
"DatabaseSettings": { "Provider": "postgresql" },
"ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=financedb;Username=finance_user;Password=CHANGE-ME" }
```

### MySQL / MariaDB

```json
"DatabaseSettings": { "Provider": "mysql" },
"ConnectionStrings": { "DefaultConnection": "Server=localhost;Port=3306;Database=financedb;User=finance_user;Password=CHANGE-ME" }
```

> The schema migration sets `utf8mb4_unicode_ci` on `Username`, `Email` and `Categories.Name` so that uniqueness constraints are case-insensitive regardless of the server's default collation. No manual collation configuration is required.

Each migration file is named `V{NNN}__{description}.sql` (e.g. `V001__initial_schema.sql`). Migrations run in version order and are recorded in `SchemaVersions` after successful execution.

## Operational Notes

### Secrets

- `JwtSettings:SecretKey` must be at least 32 characters. Use environment variables or a secrets manager in production — never commit the real value to source control.
- Use `appsettings.example.json` as a template; copy it to `appsettings.json` locally (`.gitignore` excludes the live file).

### Health & Monitoring

- `GET /health` checks both application liveness and database reachability. Returns `Healthy` when the DB can be queried.
- Every response carries `X-Correlation-Id` (ASP.NET Core trace ID) for log correlation.
- Audit events (login, role changes, user deactivation, API key creation/revocation) are emitted as structured log entries with user IDs. Wire the ASP.NET Core logging pipeline to your preferred sink (console, file, OpenTelemetry) in `appsettings.json`.

### Swagger UI

Swagger is enabled automatically in the `Development` environment. In other environments set `SwaggerSettings:Enabled: true` in `appsettings.json` to expose it.

### Scaling

- PostgreSQL and MySQL support concurrent multi-instance deployments safely. Admin invariants are enforced with Serializable transactions and automatic retry.
- SQLite is suitable for single-instance or low-concurrency deployments only. It does not support concurrent writes from multiple processes.
- The auth rate limiter is partitioned by client IP and is process-local — each application instance maintains its own counters. The limit is configurable via `RateLimitSettings:AuthPermitLimit` and `AuthWindowMinutes` (default: 10 requests/minute). For multi-instance deployments, use a shared store (Redis) or a gateway-level rate limiter instead.
- **Reverse proxy / load balancer:** When the API runs behind a reverse proxy, set `ForwardedHeadersSettings:Enabled: true` and list the proxy IP(s) in `TrustedProxies`. This enables `X-Forwarded-For` processing so the rate limiter sees the real client IP instead of the proxy's IP. Only explicitly listed IPs are trusted — unlisted sources cannot spoof the header.
  - **Single proxy (default):** `ForwardLimit: 1` (the default) processes exactly one hop from `X-Forwarded-For`. This is correct when a single trusted reverse proxy (e.g. nginx, Traefik) sits directly in front of the API.
  - **Proxy chains / multiple load balancers:** If the request traverses more than one proxy before reaching the API (e.g. a CDN in front of a load balancer), increase `ForwardLimit` to match the number of hops, or set it to `0` for unlimited. Each proxy in the chain must be listed in `TrustedProxies`; otherwise `RemoteIpAddress` will reflect an inner proxy IP rather than the real client, causing the rate limiter to share a bucket across multiple clients.

## License

MIT
