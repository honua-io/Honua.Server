-- ============================================================================
-- MySQL: Add Optimistic Locking Support
-- ============================================================================
-- This script adds row_version columns and triggers for optimistic concurrency control
-- Apply this to your feature tables for version tracking
-- ============================================================================

-- ============================================================================
-- Example: Apply to a feature table
-- ============================================================================
-- Replace 'your_schema.your_table' with your actual table names

-- Step 1: Add row_version column if it doesn't exist
/*
SET @table_schema = 'your_schema';
SET @table_name = 'your_table';
SET @column_check = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = @table_schema
    AND table_name = @table_name
    AND column_name = 'row_version'
);

-- Add column if it doesn't exist
SET @sql = IF(
    @column_check = 0,
    CONCAT('ALTER TABLE ', @table_schema, '.', @table_name,
           ' ADD COLUMN row_version BIGINT NOT NULL DEFAULT 1 COMMENT ''Version number for optimistic concurrency control'''),
    'SELECT ''Column already exists'' AS result'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
*/

-- Step 2: Create trigger to auto-increment version
/*
DELIMITER $$

DROP TRIGGER IF EXISTS trg_your_table_version$$

CREATE TRIGGER trg_your_table_version
BEFORE UPDATE ON your_schema.your_table
FOR EACH ROW
BEGIN
    -- Increment version on update
    SET NEW.row_version = OLD.row_version + 1;
END$$

DELIMITER ;
*/

-- Step 3: Create index on row_version for efficient lookups
/*
CREATE INDEX idx_your_table_row_version
ON your_schema.your_table(row_version);
*/

-- ============================================================================
-- Batch Script for Multiple Tables
-- ============================================================================
-- If you have multiple tables, you can create a procedure to apply to all:
/*
DELIMITER $$

DROP PROCEDURE IF EXISTS add_optimistic_locking_to_table$$

CREATE PROCEDURE add_optimistic_locking_to_table(
    IN p_schema VARCHAR(64),
    IN p_table VARCHAR(64)
)
BEGIN
    DECLARE column_exists INT DEFAULT 0;
    DECLARE trigger_exists INT DEFAULT 0;

    -- Check if column exists
    SELECT COUNT(*) INTO column_exists
    FROM information_schema.columns
    WHERE table_schema = p_schema
    AND table_name = p_table
    AND column_name = 'row_version';

    -- Add column if it doesn't exist
    IF column_exists = 0 THEN
        SET @sql = CONCAT('ALTER TABLE ', p_schema, '.', p_table,
                         ' ADD COLUMN row_version BIGINT NOT NULL DEFAULT 1');
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        SELECT CONCAT('Added row_version column to ', p_schema, '.', p_table) AS result;
    END IF;

    -- Check if trigger exists
    SELECT COUNT(*) INTO trigger_exists
    FROM information_schema.triggers
    WHERE trigger_schema = p_schema
    AND event_object_table = p_table
    AND trigger_name = CONCAT('trg_', p_table, '_version');

    -- Create trigger if it doesn't exist
    IF trigger_exists = 0 THEN
        SET @sql = CONCAT(
            'CREATE TRIGGER trg_', p_table, '_version ',
            'BEFORE UPDATE ON ', p_schema, '.', p_table, ' ',
            'FOR EACH ROW ',
            'BEGIN ',
                'SET NEW.row_version = OLD.row_version + 1; ',
            'END'
        );
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        SELECT CONCAT('Created trigger for ', p_schema, '.', p_table) AS result;
    END IF;

    -- Create index
    SET @sql = CONCAT('CREATE INDEX IF NOT EXISTS idx_', p_table, '_row_version ',
                     'ON ', p_schema, '.', p_table, '(row_version)');
    PREPARE stmt FROM @sql;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
END$$

DELIMITER ;

-- Usage: CALL add_optimistic_locking_to_table('your_schema', 'your_table');
*/

-- ============================================================================
-- Usage Pattern for Applications
-- ============================================================================
-- 1. SELECT: Get current row_version value
--    SELECT id, name, ..., row_version FROM your_table WHERE id = ?
--
-- 2. UPDATE: Include row_version in WHERE clause
--    UPDATE your_table
--    SET name = ?, ...
--    WHERE id = ? AND row_version = ?
--
-- 3. CHECK: If ROW_COUNT() = 0, throw concurrency exception
--    SELECT ROW_COUNT() into affected_rows;
--    IF affected_rows = 0 THEN
--        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Concurrency conflict detected';
--    END IF;

-- ============================================================================
-- Verification Query
-- ============================================================================
-- Check which tables have row_version columns:
/*
SELECT
    table_schema AS SchemaName,
    table_name AS TableName,
    column_name AS ColumnName,
    data_type AS DataType,
    column_comment AS Comment
FROM information_schema.columns
WHERE column_name = 'row_version'
    AND table_schema NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
ORDER BY table_schema, table_name;
*/
