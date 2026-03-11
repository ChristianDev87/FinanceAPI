CREATE TABLE IF NOT EXISTS Roles (
    Name VARCHAR(50) PRIMARY KEY
);

INSERT IGNORE INTO Roles (Name) VALUES ('Admin');
INSERT IGNORE INTO Roles (Name) VALUES ('User');

CREATE TABLE IF NOT EXISTS Users (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    Username     VARCHAR(100) NOT NULL,
    Email        VARCHAR(255) NOT NULL,
    PasswordHash TEXT NOT NULL,
    RoleName     VARCHAR(50) NOT NULL DEFAULT 'User',
    IsActive     TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt    VARCHAR(50) NOT NULL DEFAULT (DATE_FORMAT(NOW(), '%Y-%m-%d %H:%i:%s')),
    UNIQUE (Username),
    UNIQUE (Email),
    FOREIGN KEY (RoleName) REFERENCES Roles(Name)
);

CREATE TABLE IF NOT EXISTS ApiKeys (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    UserId           INT NOT NULL,
    KeyHash          VARCHAR(255) NOT NULL UNIQUE,
    Name             VARCHAR(255) NOT NULL,
    IsActive         TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt        VARCHAR(50) NOT NULL DEFAULT (DATE_FORMAT(NOW(), '%Y-%m-%d %H:%i:%s')),
    CreatedByAdminId INT,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (CreatedByAdminId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS Categories (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    UserId    INT NOT NULL,
    Name      VARCHAR(255) NOT NULL,
    Color     VARCHAR(20) NOT NULL DEFAULT '#1abc9c',
    Type      ENUM('income', 'expense') NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    UNIQUE (UserId, Name),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Transactions (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    UserId      INT NOT NULL,
    Amount      DECIMAL(18,2) NOT NULL,
    Type        ENUM('income', 'expense') NOT NULL,
    CategoryId  INT,
    Date        VARCHAR(20) NOT NULL,
    Description TEXT,
    CreatedAt   VARCHAR(50) NOT NULL DEFAULT (DATE_FORMAT(NOW(), '%Y-%m-%d %H:%i:%s')),
    CONSTRAINT chk_amount CHECK (Amount > 0),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
);

-- MySQL 8.0 does not support CREATE INDEX IF NOT EXISTS;
-- these will be skipped automatically if they already exist.
CREATE INDEX idx_transactions_user_date ON Transactions(UserId, Date);
CREATE INDEX idx_categories_user        ON Categories(UserId);
CREATE INDEX idx_apikeys_hash           ON ApiKeys(KeyHash);
