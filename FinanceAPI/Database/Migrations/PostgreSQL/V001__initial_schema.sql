CREATE TABLE IF NOT EXISTS Roles (
    Name TEXT PRIMARY KEY
);

INSERT INTO Roles (Name) VALUES ('Admin') ON CONFLICT DO NOTHING;
INSERT INTO Roles (Name) VALUES ('User')  ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS Users (
    Id           SERIAL PRIMARY KEY,
    Username     TEXT NOT NULL,
    Email        TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    RoleName     TEXT NOT NULL DEFAULT 'User' REFERENCES Roles(Name),
    IsActive     BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt    TEXT NOT NULL DEFAULT TO_CHAR(NOW(), 'YYYY-MM-DD HH24:MI:SS')
);

-- Case-insensitive uniqueness (equivalent to SQLite COLLATE NOCASE UNIQUE)
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username_lower ON Users (LOWER(Username));
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_lower    ON Users (LOWER(Email));

CREATE TABLE IF NOT EXISTS ApiKeys (
    Id               SERIAL PRIMARY KEY,
    UserId           INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    KeyHash          TEXT NOT NULL UNIQUE,
    Name             TEXT NOT NULL,
    IsActive         BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt        TEXT NOT NULL DEFAULT TO_CHAR(NOW(), 'YYYY-MM-DD HH24:MI:SS'),
    CreatedByAdminId INTEGER REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS Categories (
    Id        SERIAL PRIMARY KEY,
    UserId    INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Name      TEXT NOT NULL,
    Color     TEXT NOT NULL DEFAULT '#1abc9c',
    Type      TEXT NOT NULL CHECK (Type IN ('income', 'expense')),
    SortOrder INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Transactions (
    Id          SERIAL PRIMARY KEY,
    UserId      INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Amount      NUMERIC(18,2) NOT NULL CHECK (Amount > 0),
    Type        TEXT NOT NULL CHECK (Type IN ('income', 'expense')),
    CategoryId  INTEGER REFERENCES Categories(Id) ON DELETE SET NULL,
    Date        TEXT NOT NULL,
    Description TEXT,
    CreatedAt   TEXT NOT NULL DEFAULT TO_CHAR(NOW(), 'YYYY-MM-DD HH24:MI:SS')
);

CREATE INDEX IF NOT EXISTS idx_transactions_user_date ON Transactions (UserId, Date);
CREATE INDEX IF NOT EXISTS idx_categories_user        ON Categories (UserId);
CREATE INDEX IF NOT EXISTS idx_apikeys_hash           ON ApiKeys (KeyHash);

-- Case-insensitive uniqueness for category names per user
CREATE UNIQUE INDEX IF NOT EXISTS idx_categories_user_name_lower ON Categories (UserId, LOWER(Name));
