-- Add PasswordVersion to Users for JWT invalidation after password change.
-- Default 0 means never changed. UpdatePasswordAsync increments this on every change.
ALTER TABLE Users ADD COLUMN PasswordVersion INTEGER NOT NULL DEFAULT 0;

-- Enforce at most one active API key per user at the schema level.
-- Combined with the serializable CreateApiKeyAsync transaction this prevents
-- parallel requests from leaving multiple active keys.
CREATE UNIQUE INDEX IF NOT EXISTS idx_apikeys_active_user ON ApiKeys (UserId) WHERE IsActive = 1;
