CREATE TABLE IF NOT EXISTS Roles (
    Name TEXT PRIMARY KEY
);

INSERT OR IGNORE INTO Roles (Name) VALUES ('Admin');
INSERT OR IGNORE INTO Roles (Name) VALUES ('User');

CREATE TABLE IF NOT EXISTS Users (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Username     TEXT NOT NULL UNIQUE COLLATE NOCASE,
    Email        TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    RoleName     TEXT NOT NULL DEFAULT 'User' REFERENCES Roles(Name),
    CreatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS ApiKeys (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId           INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    KeyHash          TEXT NOT NULL UNIQUE,
    Name             TEXT NOT NULL,
    IsActive         INTEGER NOT NULL DEFAULT 1,
    CreatedAt        TEXT NOT NULL DEFAULT (datetime('now')),
    CreatedByAdminId INTEGER REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS Categories (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId    INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Name      TEXT NOT NULL,
    Color     TEXT NOT NULL DEFAULT '#1abc9c',
    Type      TEXT NOT NULL CHECK(Type IN ('income', 'expense')),
    SortOrder INTEGER NOT NULL DEFAULT 0,
    UNIQUE(UserId, Name COLLATE NOCASE)
);

CREATE TABLE IF NOT EXISTS Transactions (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId      INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Amount      REAL NOT NULL CHECK(Amount > 0),
    Type        TEXT NOT NULL CHECK(Type IN ('income', 'expense')),
    CategoryId  INTEGER REFERENCES Categories(Id) ON DELETE SET NULL,
    Date        TEXT NOT NULL,
    Description TEXT,
    CreatedAt   TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_transactions_user_date ON Transactions(UserId, Date);
CREATE INDEX IF NOT EXISTS idx_categories_user        ON Categories(UserId);
CREATE INDEX IF NOT EXISTS idx_apikeys_hash           ON ApiKeys(KeyHash);

-- Migration: add IsActive column to Users (ignored if already exists)
ALTER TABLE Users ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;

-- Migration: enforce case-insensitive uniqueness for category names (ignored if already exists)
CREATE UNIQUE INDEX IF NOT EXISTS idx_categories_user_name_nocase ON Categories (UserId, Name COLLATE NOCASE);
