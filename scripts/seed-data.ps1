#!/usr/bin/env pwsh
# Load test data into the Honua Server database (Windows PowerShell)

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

# Get project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Print-Status "Loading test data into database..."
Write-Host ""

# Check if PostgreSQL container is running
$postgresRunning = & docker ps | Select-String "honua-postgres"
if (-not $postgresRunning) {
    Print-Warning "PostgreSQL container not running. Starting Docker containers..."
    & docker compose up -d postgres
    Start-Sleep -Seconds 5
}

# Check if seed data file exists
$seedFile = Join-Path $ProjectRoot ".env.seed"
if (Test-Path $seedFile) {
    Print-Status "Found .env.seed file"

    # Use docker-compose.seed.yml if available
    $seedCompose = Join-Path $ProjectRoot "docker-compose.seed.yml"
    if (Test-Path $seedCompose) {
        Print-Status "Using docker-compose.seed.yml..."
        & docker compose -f docker-compose.seed.yml up --build --exit-code-from seed
    } else {
        Print-Warning "docker-compose.seed.yml not found"
        Print-Warning "Please create seed data manually or use the Honua CLI"
    }
} else {
    Print-Warning ".env.seed file not found"
    Write-Host ""
    Write-Host "To seed data, you can:"
    Write-Host "  1. Copy .env.seed.example to .env.seed and customize"
    Write-Host "  2. Run: docker compose -f docker-compose.seed.yml up"
    Write-Host ""
    Write-Host "Or use the Honua CLI to import data:"
    Write-Host "  • Import GeoJSON: dotnet run --project src\Honua.Cli -- import geojson data.json"
    Write-Host "  • Import Shapefile: dotnet run --project src\Honua.Cli -- import shapefile data.shp"
    Write-Host "  • Import GeoPackage: dotnet run --project src\Honua.Cli -- import gpkg data.gpkg"
}

Write-Host ""
Print-Success "Seed data operation completed!"
Write-Host ""
