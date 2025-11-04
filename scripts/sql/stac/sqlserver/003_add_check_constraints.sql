-- Migration: Add CHECK Constraints to STAC Tables
-- Description: Adds CHECK constraints for data validation on stac_items table
-- Database: SQL Server
-- Date: 2025-10-18
-- Version: 1.0

-- Add CHECK constraint for temporal fields
-- Ensures either datetime OR both start_datetime and end_datetime are present
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'chk_stac_items_temporal'
    AND parent_object_id = OBJECT_ID('stac_items')
)
BEGIN
    ALTER TABLE stac_items ADD CONSTRAINT chk_stac_items_temporal
        CHECK (
            datetime IS NOT NULL OR
            (start_datetime IS NOT NULL AND end_datetime IS NOT NULL)
        );
END
GO

-- Add CHECK constraint for temporal order
-- Ensures start_datetime is before or equal to end_datetime
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'chk_stac_items_temporal_order'
    AND parent_object_id = OBJECT_ID('stac_items')
)
BEGIN
    ALTER TABLE stac_items ADD CONSTRAINT chk_stac_items_temporal_order
        CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime);
END
GO

-- Add extended properties to document the constraints
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Validates STAC temporal requirement: either datetime (instant) or start_datetime+end_datetime (range) must be present',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'stac_items',
    @level2type = N'CONSTRAINT', @level2name = 'chk_stac_items_temporal';
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Ensures temporal range is valid: start_datetime must be before or equal to end_datetime',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'stac_items',
    @level2type = N'CONSTRAINT', @level2name = 'chk_stac_items_temporal_order';
GO
