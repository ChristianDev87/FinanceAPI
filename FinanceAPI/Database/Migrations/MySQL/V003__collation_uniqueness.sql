-- Enforce case-insensitive uniqueness on Username, Email and Category Name
-- independently of the server's default collation.
-- Without explicit collation a case-sensitive server default (e.g. utf8mb4_bin)
-- would allow 'Alice' and 'alice' to coexist, breaking LOWER()-based lookups.
ALTER TABLE Users
    MODIFY Username VARCHAR(100) NOT NULL COLLATE utf8mb4_unicode_ci,
    MODIFY Email    VARCHAR(255) NOT NULL COLLATE utf8mb4_unicode_ci;

ALTER TABLE Categories
    MODIFY Name VARCHAR(255) NOT NULL COLLATE utf8mb4_unicode_ci;
