@echo off
REM OGC SensorThings API Database Setup Script for Windows
REM Creates PostgreSQL database with PostGIS and runs SensorThings schema migration

setlocal enabledelayedexpansion

REM Configuration
set DB_NAME=honua_sensors
set DB_USER=postgres
set DB_HOST=localhost
set DB_PORT=5432

echo ========================================
echo OGC SensorThings API Database Setup
echo ========================================
echo.

REM Check if PostgreSQL is accessible
echo Checking PostgreSQL availability...
pg_isready -h %DB_HOST% -p %DB_PORT% >nul 2>&1
if errorlevel 1 (
    echo ERROR: PostgreSQL is not running on %DB_HOST%:%DB_PORT%
    echo Please start PostgreSQL and try again.
    pause
    exit /b 1
)
echo [OK] PostgreSQL is running
echo.

REM Check if database exists
echo Checking if database exists...
psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -lqt | findstr /C:"%DB_NAME%" >nul 2>&1
if not errorlevel 1 (
    echo Database %DB_NAME% already exists.
    set /p RECREATE="Drop and recreate? (y/N): "
    if /i "!RECREATE!"=="y" (
        echo Dropping existing database...
        dropdb -h %DB_HOST% -p %DB_PORT% -U %DB_USER% %DB_NAME%
        echo [OK] Database dropped
    ) else (
        echo Using existing database.
        goto :skip_create
    )
)

echo Creating database: %DB_NAME%
createdb -h %DB_HOST% -p %DB_PORT% -U %DB_USER% %DB_NAME%
if errorlevel 1 (
    echo ERROR: Failed to create database
    pause
    exit /b 1
)
echo [OK] Database created: %DB_NAME%

:skip_create
echo.

REM Enable PostGIS
echo Enabling PostGIS extension...
psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "CREATE EXTENSION IF NOT EXISTS postgis;" >nul 2>&1
if errorlevel 1 (
    echo ERROR: Failed to enable PostGIS
    pause
    exit /b 1
)
echo [OK] PostGIS enabled
echo.

REM Run migration
echo Running SensorThings schema migration...
set MIGRATION_FILE=src\Honua.Server.Enterprise\Sensors\Data\Migrations\001_InitialSchema.sql

if not exist "%MIGRATION_FILE%" (
    echo ERROR: Migration file not found: %MIGRATION_FILE%
    echo Please ensure you're running this script from the repository root.
    pause
    exit /b 1
)

psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f "%MIGRATION_FILE%" >nul 2>&1
if errorlevel 1 (
    echo ERROR: Schema migration failed
    pause
    exit /b 1
)
echo [OK] Schema migration completed
echo.

REM Verify schema
echo Verifying schema...
for %%t in (sta_things sta_locations sta_historical_locations sta_sensors sta_observed_properties sta_datastreams sta_observations sta_features_of_interest) do (
    psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '%%t';" | findstr /C:"1" >nul 2>&1
    if errorlevel 1 (
        echo ERROR: Table %%t not found
        pause
        exit /b 1
    )
)
echo [OK] All 8 entity tables created
echo.

REM Create sample data
set /p CREATE_SAMPLE="Create sample test data? (Y/n): "
if /i not "!CREATE_SAMPLE!"=="n" (
    echo Creating sample data...
    psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f "scripts\setup-sensorthings-db.sql" >nul 2>&1
    if not errorlevel 1 (
        echo [OK] Sample data created
    )
)
echo.

REM Display summary
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo Database Connection Details:
echo   Host:     %DB_HOST%
echo   Port:     %DB_PORT%
echo   Database: %DB_NAME%
echo   User:     %DB_USER%
echo.
echo Connection String:
echo   Host=%DB_HOST%;Port=%DB_PORT%;Database=%DB_NAME%;Username=%DB_USER%;Password=YOUR_PASSWORD
echo.
echo Update your appsettings.Development.json with the connection string above.
echo.
echo Test the API:
echo   1. Start the application: cd src\Honua.Server.Host ^&^& dotnet run
echo   2. Test service root: curl http://localhost:5000/sta/v1.1
echo   3. Test Things: curl http://localhost:5000/sta/v1.1/Things
echo.
pause
