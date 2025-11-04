-- ============================================================================
-- SQL Server: Add Optimistic Locking Support
-- ============================================================================
-- This script adds ROWVERSION columns for optimistic concurrency control
-- SQL Server's ROWVERSION is automatically managed by the database
-- ============================================================================

-- ============================================================================
-- Important: SQL Server ROWVERSION (formerly TIMESTAMP)
-- ============================================================================
-- ROWVERSION is a database-unique, monotonically increasing value
-- It's automatically updated on INSERT and UPDATE
-- Cannot be manually set or modified
-- Guaranteed to be unique across the entire database
-- Ideal for optimistic concurrency control

-- ============================================================================
-- Example: Apply to a feature table
-- ============================================================================
-- Replace 'your_schema.your_table' with your actual table names

-- Step 1: Check if column exists, then add it
/*
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('your_schema.your_table')
    AND name = 'row_version'
)
BEGIN
    ALTER TABLE your_schema.your_table
    ADD row_version ROWVERSION NOT NULL;

    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Version stamp for optimistic concurrency control - automatically managed by SQL Server',
        @level0type = N'SCHEMA', @level0name = 'your_schema',
        @level1type = N'TABLE',  @level1name = 'your_table',
        @level2type = N'COLUMN', @level2name = 'row_version';
END
GO
*/

-- Step 2: Create index for efficient lookups
/*
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('your_schema.your_table')
    AND name = 'idx_your_table_row_version'
)
BEGIN
    CREATE NONCLUSTERED INDEX idx_your_table_row_version
        ON your_schema.your_table(row_version);
END
GO
*/

-- ============================================================================
-- Alternative: Using BIGINT with DEFAULT CONSTRAINT (if ROWVERSION not suitable)
-- ============================================================================
-- If you need to replicate or have other constraints, use BIGINT with trigger:
/*
-- Add column
ALTER TABLE your_schema.your_table
ADD row_version BIGINT NOT NULL DEFAULT 1;
GO

-- Create trigger to auto-increment
CREATE OR ALTER TRIGGER trg_your_table_version
ON your_schema.your_table
FOR UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET row_version = t.row_version + 1
    FROM your_schema.your_table t
    INNER JOIN inserted i ON t.id = i.id
    WHERE t.row_version = (SELECT row_version FROM deleted WHERE id = i.id);
END
GO
*/

-- ============================================================================
-- Usage Pattern for Applications
-- ============================================================================
-- When using ROWVERSION:
-- 1. SELECT: Get current row_version value
--    SELECT id, name, ..., row_version FROM your_table WHERE id = @id
--
-- 2. UPDATE: Include row_version in WHERE clause
--    UPDATE your_table
--    SET name = @name, ...
--    WHERE id = @id AND row_version = @expectedVersion
--
-- 3. CHECK: If @@ROWCOUNT = 0, throw concurrency exception
--    IF @@ROWCOUNT = 0
--        THROW 50001, 'Concurrency conflict detected', 1;

-- ============================================================================
-- Verification Query
-- ============================================================================
-- Check which tables have row_version columns:
/*
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE
        WHEN ty.name = 'timestamp' THEN 'ROWVERSION (auto-managed)'
        WHEN ty.name = 'bigint' THEN 'BIGINT (trigger-managed)'
        ELSE 'Other'
    END AS VersionType
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name = 'row_version'
ORDER BY s.name, t.name;
*/
