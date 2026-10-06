-- ============================================================
-- Migration 002: Spatial Index Optimizations
-- Adds composite indexes for common query patterns
-- ============================================================

USE scripox_db;

-- Composite index for map queries: city + status + coordinates
ALTER TABLE companies
  ADD INDEX idx_co_city_status (city, status_id),
  ADD INDEX idx_co_country_city (country, city);

-- Full-text search on company name and address
ALTER TABLE companies
  ADD FULLTEXT INDEX ft_name_address (name, address);

-- Index for date range queries on extraction jobs
ALTER TABLE extraction_jobs
  ADD INDEX idx_jobs_date (created_at);

-- Index for audit log time-based queries
ALTER TABLE audit_logs
  ADD INDEX idx_al_entity_time (entity, created_at);

SELECT 'Migration 002 completed successfully.' AS result;
