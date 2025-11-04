-- Initialize Honua SQL Server database
USE master;
GO

-- Create Honua database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'Honua')
BEGIN
    CREATE DATABASE Honua;
END
GO

USE Honua;
GO

-- Create a sample spatial table for testing
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[test_spatial]') AND type in (N'U'))
BEGIN
    CREATE TABLE test_spatial (
        id INT IDENTITY(1,1) PRIMARY KEY,
        name NVARCHAR(100),
        geom GEOMETRY,
        created_at DATETIME2 DEFAULT GETDATE()
    );
END
GO

-- Insert sample data
DELETE FROM test_spatial;
INSERT INTO test_spatial (name, geom) VALUES
    ('Test Point 1', GEOMETRY::STGeomFromText('POINT(-74.006 40.7128)', 4326)),
    ('Test Point 2', GEOMETRY::STGeomFromText('POINT(-73.935 40.7305)', 4326));
GO

-- Create spatial index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_test_spatial_geom')
BEGIN
    CREATE SPATIAL INDEX idx_test_spatial_geom ON test_spatial(geom);
END
GO