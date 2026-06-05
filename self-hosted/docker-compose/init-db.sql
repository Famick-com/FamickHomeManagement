-- Initialize database for self-hosted deployment
-- This script runs automatically when the PostgreSQL container starts for the first time
--
-- NOTE: The Tenants table and initial data are created by EF Core migrations.
-- This file is reserved for any additional database initialization that may be needed.

-- PostgreSQL extensions (if needed)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- The application will automatically:
-- 1. Run EF Core migrations on startup
-- 2. Create the fixed tenant for self-hosted mode
-- 3. Allow user registration via the API
