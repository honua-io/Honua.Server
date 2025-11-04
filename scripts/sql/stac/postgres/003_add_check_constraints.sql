-- Migration: Add CHECK Constraints to STAC Tables
-- Description: Adds CHECK constraints for data validation on stac_items table
-- Database: PostgreSQL
-- Date: 2025-10-18
-- Version: 1.0

-- Add CHECK constraint for temporal fields
-- Ensures either datetime OR both start_datetime and end_datetime are present
ALTER TABLE stac_items ADD CONSTRAINT chk_stac_items_temporal
    CHECK (
        datetime IS NOT NULL OR
        (start_datetime IS NOT NULL AND end_datetime IS NOT NULL)
    );

-- Add CHECK constraint for temporal order
-- Ensures start_datetime is before or equal to end_datetime
ALTER TABLE stac_items ADD CONSTRAINT chk_stac_items_temporal_order
    CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime);

-- Add comments to document the constraints
COMMENT ON CONSTRAINT chk_stac_items_temporal ON stac_items IS
    'Validates STAC temporal requirement: either datetime (instant) or start_datetime+end_datetime (range) must be present';

COMMENT ON CONSTRAINT chk_stac_items_temporal_order ON stac_items IS
    'Ensures temporal range is valid: start_datetime must be before or equal to end_datetime';
