#!/usr/bin/env pwsh
# Reset the Honua Server database (WARNING: Destroys all data!)

$ErrorActionPreference = "Stop"

function Print-Status {
    param([string]$Message)
    Write-Host "==> " -ForegroundColor Blue -NoNewline
    Write-Host $Message
}

function Print-Success {
    param([string]$Message)
    Write-Host "✓ " -ForegroundColor Green -NoNewline
    Write-Host $Message
}

function Print-Warning {
    param([string]$Message)
    Write-Host "! " -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Print-Error {
    param([string]$Message)
    Write-Host "✗ " -ForegroundColor Red -NoNewline
    Write-Host $Message
}

# Get project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Write-Host ""
Write-Host "WARNING: This will destroy all data in the database!" -ForegroundColor Red
Write-Host ""
$confirmation = Read-Host "Are you sure you want to reset the database? (yes/no)"

if ($confirmation -ne "yes") {
    Write-Host "Database reset cancelled."
    exit 0
}

Print-Status "Stopping Honua Server if running..."
& docker compose stop honua-server 2>$null

Print-Status "Dropping and recreating PostgreSQL database..."
& docker exec honua-postgres psql -U honua -c "DROP DATABASE IF EXISTS honua;" 2>$null
& docker exec honua-postgres psql -U honua -c "CREATE DATABASE honua;"
& docker exec honua-postgres psql -U honua -d honua -c "CREATE EXTENSION IF NOT EXISTS postgis;"

Print-Status "Clearing Redis cache..."
& docker exec honua-redis redis-cli FLUSHALL

Write-Host ""
Print-Success "Database has been reset!"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  • Run migrations: dotnet ef database update --project src\Honua.Server.Host"
Write-Host "  • Load test data: .\scripts\seed-data.ps1"
Write-Host "  • Start server: dotnet run --project src\Honua.Server.Host"
Write-Host ""
