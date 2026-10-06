-- ============================================================
-- ScripOx Database Schema
-- Ox Tech — oxtech.uk
-- MySQL 8.0+ required (spatial SRID 4326 support)
-- ============================================================

CREATE DATABASE IF NOT EXISTS scripox_db
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE scripox_db;

SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================
-- 1. roles
-- ============================================================
CREATE TABLE IF NOT EXISTS roles (
  id         INT            AUTO_INCREMENT PRIMARY KEY,
  name       VARCHAR(50)    NOT NULL UNIQUE,
  name_ar    VARCHAR(100),
  created_at DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 2. users
-- ============================================================
CREATE TABLE IF NOT EXISTS users (
  id            INT            AUTO_INCREMENT PRIMARY KEY,
  username      VARCHAR(100)   NOT NULL UNIQUE,
  email         VARCHAR(255)   NOT NULL UNIQUE,
  password_hash VARCHAR(255)   NOT NULL,
  full_name     VARCHAR(200),
  role_id       INT            NOT NULL,
  is_active     TINYINT(1)     NOT NULL DEFAULT 1,
  language      ENUM('en','ar') NOT NULL DEFAULT 'en',
  avatar_url    VARCHAR(500),
  created_at    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  last_login    DATETIME,
  CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 3. sources
-- ============================================================
CREATE TABLE IF NOT EXISTS sources (
  id          INT            AUTO_INCREMENT PRIMARY KEY,
  name        VARCHAR(200)   NOT NULL,
  type        ENUM('web','api','csv','manual') NOT NULL DEFAULT 'web',
  base_url    VARCHAR(500),
  connector   VARCHAR(100),
  config_json JSON,
  is_active   TINYINT(1)     NOT NULL DEFAULT 1,
  created_by  INT,
  created_at  DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_sources_user FOREIGN KEY (created_by) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 4. field_mappings  (ربط أعمدة المصدر بالحقول الموحدة)
-- ============================================================
CREATE TABLE IF NOT EXISTS field_mappings (
  id           INT           AUTO_INCREMENT PRIMARY KEY,
  source_id    INT           NOT NULL,
  source_field VARCHAR(200)  NOT NULL,
  target_field VARCHAR(200)  NOT NULL,
  transform    VARCHAR(100)  COMMENT 'e.g. trim, phone_normalize, to_lower',
  sort_order   INT           NOT NULL DEFAULT 0,
  CONSTRAINT fk_fm_source FOREIGN KEY (source_id) REFERENCES sources(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 5. extraction_jobs
-- ============================================================
CREATE TABLE IF NOT EXISTS extraction_jobs (
  id           INT            AUTO_INCREMENT PRIMARY KEY,
  source_id    INT            NOT NULL,
  triggered_by INT            NOT NULL,
  status       ENUM('pending','running','completed','failed','cancelled') NOT NULL DEFAULT 'pending',
  params_json  JSON,
  total_found  INT            NOT NULL DEFAULT 0,
  total_saved  INT            NOT NULL DEFAULT 0,
  total_dupes  INT            NOT NULL DEFAULT 0,
  total_errors INT            NOT NULL DEFAULT 0,
  progress_pct TINYINT        NOT NULL DEFAULT 0,
  started_at   DATETIME,
  ended_at     DATETIME,
  error_msg    TEXT,
  created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_jobs_status (status),
  INDEX idx_jobs_source (source_id),
  CONSTRAINT fk_jobs_source FOREIGN KEY (source_id)      REFERENCES sources(id),
  CONSTRAINT fk_jobs_user   FOREIGN KEY (triggered_by)   REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 6. raw_records
-- ============================================================
CREATE TABLE IF NOT EXISTS raw_records (
  id          BIGINT         AUTO_INCREMENT PRIMARY KEY,
  job_id      INT            NOT NULL,
  raw_json    JSON           NOT NULL,
  status      ENUM('pending','approved','rejected','duplicate') NOT NULL DEFAULT 'pending',
  reviewed_by INT,
  reviewed_at DATETIME,
  reject_reason VARCHAR(500),
  created_at  DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_raw_job    (job_id),
  INDEX idx_raw_status (status),
  CONSTRAINT fk_raw_job  FOREIGN KEY (job_id)      REFERENCES extraction_jobs(id),
  CONSTRAINT fk_raw_user FOREIGN KEY (reviewed_by) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 7. company_statuses
-- ============================================================
CREATE TABLE IF NOT EXISTS company_statuses (
  id         INT            AUTO_INCREMENT PRIMARY KEY,
  name       VARCHAR(100)   NOT NULL,
  name_ar    VARCHAR(100),
  color      VARCHAR(7)     NOT NULL DEFAULT '#6C757D',
  is_final   TINYINT(1)     NOT NULL DEFAULT 0,
  sort_order INT            NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 8. companies  (الجدول الرئيسي)
-- ============================================================
CREATE TABLE IF NOT EXISTS companies (
  id             BIGINT         AUTO_INCREMENT PRIMARY KEY,

  -- Basic Info
  name           VARCHAR(500)   NOT NULL,
  name_ar        VARCHAR(500),
  category       VARCHAR(200),
  description    TEXT,

  -- Contact
  phone          VARCHAR(50),
  phone2         VARCHAR(50),
  email          VARCHAR(255),
  website        VARCHAR(500),

  -- Address
  address        TEXT,
  city           VARCHAR(200),
  region         VARCHAR(200),
  country        VARCHAR(100)   NOT NULL DEFAULT 'UK',
  postal_code    VARCHAR(20),

  -- Coordinates
  x              DECIMAL(11,7)  COMMENT 'longitude',
  y              DECIMAL(10,7)  COMMENT 'latitude',
  location       POINT          NOT NULL SRID 4326,

  -- Source traceability
  source_id      INT,
  source_name    VARCHAR(200),
  source_url     VARCHAR(500),
  raw_record_id  BIGINT,

  -- Workflow
  status_id      INT,
  assigned_to    INT,
  priority       ENUM('low','medium','high') NOT NULL DEFAULT 'medium',
  is_verified    TINYINT(1)     NOT NULL DEFAULT 0,
  reviewed_at    DATETIME,

  -- Meta
  created_by     INT,
  created_at     DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at     DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  SPATIAL INDEX sx_location (location),
  INDEX idx_co_city     (city),
  INDEX idx_co_country  (country),
  INDEX idx_co_status   (status_id),
  INDEX idx_co_assigned (assigned_to),
  INDEX idx_co_priority (priority),
  INDEX idx_co_category (category),

  CONSTRAINT fk_co_source   FOREIGN KEY (source_id)    REFERENCES sources(id),
  CONSTRAINT fk_co_status   FOREIGN KEY (status_id)    REFERENCES company_statuses(id),
  CONSTRAINT fk_co_assigned FOREIGN KEY (assigned_to)  REFERENCES users(id),
  CONSTRAINT fk_co_creator  FOREIGN KEY (created_by)   REFERENCES users(id),
  CONSTRAINT fk_co_raw      FOREIGN KEY (raw_record_id) REFERENCES raw_records(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 9. company_links
-- ============================================================
CREATE TABLE IF NOT EXISTS company_links (
  id         INT            AUTO_INCREMENT PRIMARY KEY,
  company_id BIGINT         NOT NULL,
  platform   VARCHAR(100)   NOT NULL COMMENT 'e.g. linkedin, facebook, google_maps',
  url        VARCHAR(500)   NOT NULL,
  CONSTRAINT fk_cl_company FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 10. tags
-- ============================================================
CREATE TABLE IF NOT EXISTS tags (
  id    INT           AUTO_INCREMENT PRIMARY KEY,
  name  VARCHAR(100)  NOT NULL UNIQUE,
  color VARCHAR(7)    NOT NULL DEFAULT '#0D6EFD'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 11. company_tag  (many-to-many)
-- ============================================================
CREATE TABLE IF NOT EXISTS company_tag (
  company_id BIGINT NOT NULL,
  tag_id     INT    NOT NULL,
  PRIMARY KEY (company_id, tag_id),
  CONSTRAINT fk_ct_company FOREIGN KEY (company_id) REFERENCES companies(id)  ON DELETE CASCADE,
  CONSTRAINT fk_ct_tag     FOREIGN KEY (tag_id)     REFERENCES tags(id)       ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 12. company_notes
-- ============================================================
CREATE TABLE IF NOT EXISTS company_notes (
  id         INT            AUTO_INCREMENT PRIMARY KEY,
  company_id BIGINT         NOT NULL,
  user_id    INT            NOT NULL,
  content    TEXT           NOT NULL,
  is_pinned  TINYINT(1)     NOT NULL DEFAULT 0,
  created_at DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  INDEX idx_notes_company (company_id),
  CONSTRAINT fk_notes_company FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
  CONSTRAINT fk_notes_user    FOREIGN KEY (user_id)    REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 13. company_comments  (با دعم الردود المتداخلة)
-- ============================================================
CREATE TABLE IF NOT EXISTS company_comments (
  id         INT            AUTO_INCREMENT PRIMARY KEY,
  company_id BIGINT         NOT NULL,
  user_id    INT            NOT NULL,
  content    TEXT           NOT NULL,
  parent_id  INT            COMMENT 'NULL = top-level comment, otherwise reply',
  is_edited  TINYINT(1)     NOT NULL DEFAULT 0,
  created_at DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  INDEX idx_comments_company (company_id),
  INDEX idx_comments_parent  (parent_id),
  CONSTRAINT fk_cmt_company FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
  CONSTRAINT fk_cmt_user    FOREIGN KEY (user_id)    REFERENCES users(id),
  CONSTRAINT fk_cmt_parent  FOREIGN KEY (parent_id)  REFERENCES company_comments(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 14. export_jobs
-- ============================================================
CREATE TABLE IF NOT EXISTS export_jobs (
  id           INT            AUTO_INCREMENT PRIMARY KEY,
  created_by   INT            NOT NULL,
  format       ENUM('xlsx','csv') NOT NULL DEFAULT 'xlsx',
  filters_json JSON           COMMENT 'Serialised filter state',
  columns_json JSON           COMMENT 'Selected columns list',
  file_path    VARCHAR(500),
  file_name    VARCHAR(200),
  status       ENUM('pending','processing','done','failed') NOT NULL DEFAULT 'pending',
  row_count    INT            NOT NULL DEFAULT 0,
  error_msg    TEXT,
  created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  completed_at DATETIME,
  CONSTRAINT fk_exp_user FOREIGN KEY (created_by) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 15. translations  (i18n key-value store)
-- ============================================================
CREATE TABLE IF NOT EXISTS translations (
  id       INT            AUTO_INCREMENT PRIMARY KEY,
  lang     ENUM('en','ar') NOT NULL,
  key_name VARCHAR(200)   NOT NULL,
  value    TEXT           NOT NULL,
  UNIQUE KEY uq_lang_key (lang, key_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- 16. audit_logs
-- ============================================================
CREATE TABLE IF NOT EXISTS audit_logs (
  id         BIGINT         AUTO_INCREMENT PRIMARY KEY,
  user_id    INT,
  entity     VARCHAR(100)   NOT NULL,
  entity_id  BIGINT,
  action     ENUM('create','update','delete','login','logout','export','approve','reject') NOT NULL,
  old_json   JSON,
  new_json   JSON,
  ip_address VARCHAR(50),
  user_agent VARCHAR(500),
  created_at DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_al_entity (entity, entity_id),
  INDEX idx_al_user   (user_id),
  INDEX idx_al_action (action),
  INDEX idx_al_time   (created_at),
  CONSTRAINT fk_al_user FOREIGN KEY (user_id) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;
