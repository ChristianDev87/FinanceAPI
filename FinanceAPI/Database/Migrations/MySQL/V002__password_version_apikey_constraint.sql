-- Add PasswordVersion to Users for JWT invalidation after password change.
-- Default 0 means never changed. UpdatePasswordAsync increments this on every change.
ALTER TABLE Users ADD COLUMN PasswordVersion INT NOT NULL DEFAULT 0;

-- MySQL does not support partial (filtered) unique indexes.
-- Active-key uniqueness is enforced at the application level via a
-- serializable transaction in UserService.CreateApiKeyAsync.
