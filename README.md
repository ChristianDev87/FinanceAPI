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
| `JwtSettings.SecretKey` | **Required.** At least 32 characters, keep secret |
| `JwtSettings.Issuer` | JWT issuer claim |
| `JwtSettings.Audience` | JWT audience claim |
| `JwtSettings.ExpirationHours` | Token lifetime in hours (default `24`) |
| `CorsSettings.AllowedOrigins` | Allowed frontend origins in production |
| `DefaultCategories` | Category list auto-assigned to every new user on registration |

> In **Development** mode CORS allows any origin. In **Production** only the origins listed in `CorsSettings.AllowedOrigins` are allowed.

## API Reference

All protected routes require either `Authorization: Bearer <jwt>` or `?apiKey=<key>`.

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
| GET | `/api/statistics/monthly?year={y}` | Income and expense totals per month |
| GET | `/api/statistics/categories?month={m}&year={y}&type={t}` | Totals grouped by category |

## Authentication

### JWT (Browser / Frontend)

After a successful login or registration the response contains a `token`. Include it in every subsequent request:

```
Authorization: Bearer <token>
```

### API Key (Scripts / Integrations)

API keys are generated via `POST /api/profile/apikeys` (self-service) or `POST /api/users/{id}/apikeys` (admin). The **plaintext key is returned only once** — store it securely immediately.

Pass the key as a query parameter on any protected endpoint:

```
GET /api/transactions?apiKey=<key>
```

Only the SHA-256 hash of the key is stored in the database. Creating a new key automatically deactivates all previous keys for that user.

## Roles

| Role | How assigned | Access |
|------|-------------|--------|
| `User` | Default on registration | Own profile, categories, transactions, statistics, API keys |
| `Admin` | Admin via `PUT /api/users/{id}` | Everything above + full user management |

> Role and lock-status changes take effect immediately for API key auth.
> For JWT, the token remains valid until it expires. Users with a locked account cannot log in and receive no new tokens.

## Project Structure

```
FinanceAPI/
├── Controllers/        AuthController, ProfileController, UsersController,
│                       CategoriesController, TransactionsController, StatisticsController
├── Database/           IDbConnectionFactory, ISqlDialect,
│                       SqliteConnectionFactory, PostgreSqlConnectionFactory, MySqlConnectionFactory,
│                       SqliteDialect, PostgreSqlDialect, MySqlDialect,
│                       DatabaseInitializer, schema.sql, schema.postgresql.sql, schema.mysql.sql
├── DTOs/               Auth/, Users/, ApiKeys/, Categories/,
│                       Transactions/, Statistics/, Profile/
├── Interfaces/
│   ├── Repositories/   IUserRepository, IApiKeyRepository,
│   │                   ICategoryRepository, ITransactionRepository
│   └── Services/       IAuthService, IUserService, ICategoryService,
│                       ITransactionService, IStatisticsService
├── Middleware/         ErrorHandlingMiddleware (global error → JSON)
│                       DualAuthMiddleware (JWT + API key)
├── Models/             User, ApiKey, Category, Transaction, Role
├── Repositories/       Dapper implementations
├── Services/           Business logic
└── Program.cs          DI registration, middleware pipeline
```

## Error Handling

All errors are returned as JSON by `ErrorHandlingMiddleware`:

```json
{ "error": "Human-readable message" }
```

| Exception type | HTTP status |
|----------------|-------------|
| `KeyNotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `ArgumentException` / `InvalidOperationException` | 400 Bad Request |
| Any other | 500 Internal Server Error |

## Database

The database provider is selected via `DatabaseSettings:Provider` in `appsettings.json`. The matching schema file is applied automatically on every startup — no manual migration needed.

| Provider value | Database | Schema file |
|---|---|---|
| `sqlite` (default) | SQLite | `Database/schema.sql` |
| `postgresql` / `postgres` | PostgreSQL 13+ | `Database/schema.postgresql.sql` |
| `mysql` | MySQL 8.0.13+ / MariaDB 10.6+ | `Database/schema.mysql.sql` |

### SQLite (default)

```json
"DatabaseSettings": { "Provider": "sqlite" },
"ConnectionStrings": { "DefaultConnection": "Data Source=data/finance.db" }
```

The database file is created automatically in the `data/` subfolder relative to the application root. No external server required.

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

`CREATE TABLE IF NOT EXISTS` statements are idempotent. `CREATE INDEX` failures on subsequent startups are silently skipped. `ALTER TABLE ... ADD COLUMN` migrations (SQLite) are silently skipped if the column already exists.

## License

MIT
