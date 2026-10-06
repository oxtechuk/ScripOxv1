-- ============================================================
-- Migration 001: Initial Schema
-- ScripOx — Ox Tech (oxtech.uk)
-- Run: mysql -u root -p < migrations/001_initial.sql
-- ============================================================

-- Source the full schema
SOURCE ../schema.sql;

-- Apply seeds
SOURCE ../seeds/roles.sql;
SOURCE ../seeds/statuses.sql;
SOURCE ../seeds/translations.sql;

-- Create default super_admin user (password: Admin@ScripOx2025)
-- BCrypt hash of 'Admin@ScripOx2025'
INSERT INTO users (username, email, password_hash, full_name, role_id, language)
VALUES (
  'superadmin',
  'admin@oxtech.uk',
  '$2a$12$KIXVgDV9U5R3PwMKK9z1IOhb.lUqRSJJRKe3c3bnb5c5n0lzLrqgm',
  'Super Administrator',
  1,
  'en'
);

SELECT 'Migration 001 completed successfully.' AS result;
