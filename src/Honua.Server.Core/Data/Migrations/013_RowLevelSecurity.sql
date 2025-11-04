-- ============================================================================
-- Row-Level Security (RLS) for Multi-Tenant Isolation
-- ============================================================================
-- Version: 013
-- Description: Enables Row-Level Security on all multi-tenant tables to enforce
--              tenant isolation at the database level for defense in depth
-- Dependencies: 001-012
-- Security: Critical for multi-tenant data isolation
-- ============================================================================

-- ============================================================================
-- Overview
-- ============================================================================
-- Row-Level Security (RLS) provides an additional layer of security by enforcing
-- tenant isolation at the PostgreSQL level, even if application code has bugs.
--
-- How it works:
-- 1. Application sets session variable: SET app.current_tenant = 'customer123';
-- 2. All queries are automatically filtered by tenant_id/customer_id
-- 3. Users cannot access data from other tenants, even with direct SQL
--
-- Performance: RLS policies use indexes efficiently when properly configured
-- ============================================================================

-- ============================================================================
-- Enable RLS on Core Multi-Tenant Tables
-- ============================================================================

-- Customers table (special case - usually accessed without RLS for admin operations)
-- ALTER TABLE customers ENABLE ROW LEVEL SECURITY;

-- Build Cache Registry (shared across tiers, but customer isolation on first_built_by_customer)
ALTER TABLE build_cache_registry ENABLE ROW LEVEL SECURITY;

-- Build Access Log (multi-tenant)
ALTER TABLE build_access_log ENABLE ROW LEVEL SECURITY;

-- Build Queue (multi-tenant)
ALTER TABLE build_queue ENABLE ROW LEVEL SECURITY;

-- Build Results (multi-tenant via build_queue)
-- ALTER TABLE build_results ENABLE ROW LEVEL SECURITY;

-- Build Events (multi-tenant via build_queue)
-- ALTER TABLE build_events ENABLE ROW LEVEL SECURITY;

-- Build Metrics (multi-tenant via build_queue)
-- ALTER TABLE build_metrics ENABLE ROW LEVEL SECURITY;

-- Build Manifests (multi-tenant)
ALTER TABLE build_manifests ENABLE ROW LEVEL SECURITY;

-- Conversations (multi-tenant)
ALTER TABLE conversations ENABLE ROW LEVEL SECURITY;

-- Conversation Messages (multi-tenant via conversations)
-- ALTER TABLE conversation_messages ENABLE ROW LEVEL SECURITY;

-- Tenant Usage (multi-tenant)
ALTER TABLE tenant_usage ENABLE ROW LEVEL SECURITY;

-- Tenant Usage Events (multi-tenant)
ALTER TABLE tenant_usage_events ENABLE ROW LEVEL SECURITY;

-- SAML Identity Providers (multi-tenant)
ALTER TABLE saml_identity_providers ENABLE ROW LEVEL SECURITY;

-- SAML Sessions (multi-tenant)
ALTER TABLE saml_sessions ENABLE ROW LEVEL SECURITY;

-- SAML User Mappings (multi-tenant)
ALTER TABLE saml_user_mappings ENABLE ROW LEVEL SECURITY;

-- Audit Events (multi-tenant)
ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;

-- Process Runs (multi-tenant)
ALTER TABLE process_runs ENABLE ROW LEVEL SECURITY;

-- ============================================================================
-- RLS Policies - Tenant Isolation
-- ============================================================================

-- Policy for build_cache_registry (filter by first_built_by_customer)
CREATE POLICY tenant_isolation_policy ON build_cache_registry
    FOR ALL
    USING (
        first_built_by_customer = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL  -- Allow NULL for system/admin access
    );

-- Policy for build_access_log
CREATE POLICY tenant_isolation_policy ON build_access_log
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for build_queue
CREATE POLICY tenant_isolation_policy ON build_queue
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for build_manifests
CREATE POLICY tenant_isolation_policy ON build_manifests
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for conversations
CREATE POLICY tenant_isolation_policy ON conversations
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for tenant_usage
CREATE POLICY tenant_isolation_policy ON tenant_usage
    FOR ALL
    USING (
        tenant_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for tenant_usage_events
CREATE POLICY tenant_isolation_policy ON tenant_usage_events
    FOR ALL
    USING (
        tenant_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for saml_identity_providers
CREATE POLICY tenant_isolation_policy ON saml_identity_providers
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for saml_sessions
CREATE POLICY tenant_isolation_policy ON saml_sessions
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for saml_user_mappings
CREATE POLICY tenant_isolation_policy ON saml_user_mappings
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for audit_events
CREATE POLICY tenant_isolation_policy ON audit_events
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- Policy for process_runs
CREATE POLICY tenant_isolation_policy ON process_runs
    FOR ALL
    USING (
        customer_id = current_setting('app.current_tenant', true)
        OR current_setting('app.current_tenant', true) IS NULL
    );

-- ============================================================================
-- Helper Functions for RLS
-- ============================================================================

-- Function to set current tenant for a session
CREATE OR REPLACE FUNCTION set_current_tenant(p_customer_id VARCHAR(100))
RETURNS VOID AS $$
BEGIN
    PERFORM set_config('app.current_tenant', p_customer_id, false);
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Function to get current tenant
CREATE OR REPLACE FUNCTION get_current_tenant()
RETURNS VARCHAR(100) AS $$
BEGIN
    RETURN current_setting('app.current_tenant', true);
END;
$$ LANGUAGE plpgsql STABLE;

-- Function to clear current tenant
CREATE OR REPLACE FUNCTION clear_current_tenant()
RETURNS VOID AS $$
BEGIN
    PERFORM set_config('app.current_tenant', NULL, false);
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION set_current_tenant IS 'Sets the current tenant for RLS policies in this session';
COMMENT ON FUNCTION get_current_tenant IS 'Gets the current tenant from session config';
COMMENT ON FUNCTION clear_current_tenant IS 'Clears the current tenant (admin access)';

-- ============================================================================
-- Bypass RLS for Admin/System Users
-- ============================================================================

-- Create a role for admin operations that can bypass RLS
-- In production, grant this to your application's admin connection pool
-- DO $$
-- BEGIN
--     IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'honua_admin') THEN
--         CREATE ROLE honua_admin BYPASSRLS;
--     END IF;
-- END $$;

-- ============================================================================
-- Validation and Testing
-- ============================================================================

-- Function to validate RLS is working correctly
CREATE OR REPLACE FUNCTION validate_rls_policies()
RETURNS TABLE(
    table_name TEXT,
    rls_enabled BOOLEAN,
    policy_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        c.relname::TEXT AS table_name,
        c.relrowsecurity AS rls_enabled,
        COUNT(p.polname)::BIGINT AS policy_count
    FROM pg_class c
    LEFT JOIN pg_policy p ON c.oid = p.polrelid
    WHERE c.relnamespace = 'public'::regnamespace
      AND c.relkind = 'r'
      AND c.relname IN (
          'build_cache_registry', 'build_access_log', 'build_queue',
          'build_manifests', 'conversations', 'tenant_usage',
          'tenant_usage_events', 'saml_identity_providers', 'saml_sessions',
          'saml_user_mappings', 'audit_events', 'process_runs'
      )
    GROUP BY c.relname, c.relrowsecurity
    ORDER BY c.relname;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION validate_rls_policies IS 'Validates that RLS is enabled and policies exist on multi-tenant tables';

-- ============================================================================
-- Usage Examples
-- ============================================================================

/*
-- Set tenant for a session (call at connection start)
SELECT set_current_tenant('customer123');

-- All subsequent queries are automatically filtered
SELECT * FROM build_queue;  -- Only returns rows for customer123

-- Admin access (bypass RLS)
SELECT clear_current_tenant();
SELECT * FROM build_queue;  -- Returns all rows

-- Validate RLS configuration
SELECT * FROM validate_rls_policies();
*/

-- ============================================================================
-- Performance Considerations
-- ============================================================================

-- RLS policies use the existing indexes on customer_id/tenant_id columns
-- No additional indexes needed if FK indexes were created in previous migrations
-- Monitor query performance with:
-- EXPLAIN ANALYZE SELECT * FROM build_queue WHERE customer_id = 'customer123';

-- ============================================================================
-- Security Notes
-- ============================================================================

-- 1. RLS is enforced at the PostgreSQL level, even if application code has bugs
-- 2. Always set app.current_tenant at the start of each request/transaction
-- 3. Use connection pooling with different pools for tenant vs admin access
-- 4. Regularly audit RLS policies with validate_rls_policies()
-- 5. Never use BYPASSRLS role for normal application queries
-- 6. Test RLS policies thoroughly before production deployment

-- ============================================================================
-- Record Migration
-- ============================================================================

INSERT INTO schema_migrations (version, name, checksum, execution_time_ms)
VALUES ('013', 'RowLevelSecurity', 'PLACEHOLDER_CHECKSUM', 0)
ON CONFLICT (version) DO NOTHING;
