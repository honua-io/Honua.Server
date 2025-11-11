#!/usr/bin/env pwsh
# Development Environment Setup Script for Honua Server (Windows PowerShell)
# This script sets up your local development environment with all required dependencies

$ErrorActionPreference = "Stop"

# Colors for output
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
Write-Host "========================================"
Write-Host "  Honua Server Development Setup"
Write-Host "========================================"
Write-Host ""

# Check for required dependencies
Print-Status "Checking for required dependencies..."

# Check .NET SDK
try {
    $dotnetVersion = & dotnet --version
    Print-Success ".NET SDK found: $dotnetVersion"
} catch {
    Print-Error ".NET SDK not found!"
    Write-Host "Please install .NET 9 SDK from: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
}

# Check Docker
try {
    $dockerVersion = & docker --version
    Print-Success "Docker found: $dockerVersion"
} catch {
    Print-Error "Docker not found!"
    Write-Host "Please install Docker Desktop from: https://www.docker.com/products/docker-desktop"
    exit 1
}

# Check Docker Compose
try {
    $composeVersion = & docker compose version
    Print-Success "Docker Compose found"
} catch {
    Print-Error "Docker Compose not found!"
    Write-Host "Please install Docker Compose or update Docker Desktop"
    exit 1
}

# Check Git
try {
    $gitVersion = & git --version
    Print-Success "Git found: $gitVersion"
} catch {
    Print-Error "Git not found!"
    Write-Host "Please install Git from: https://git-scm.com/downloads"
    exit 1
}

Write-Host ""

# Install Git hooks
Print-Status "Installing Git pre-commit hooks..."
$installHooksScript = Join-Path $ScriptDir "install-hooks.ps1"
if (Test-Path $installHooksScript) {
    & $installHooksScript
    Print-Success "Git hooks installed"
} else {
    Print-Warning "install-hooks.ps1 not found, skipping Git hooks installation"
}

Write-Host ""

# Start Docker containers
Print-Status "Starting Docker containers (PostgreSQL, Redis)..."
try {
    & docker compose up -d postgres redis
    Print-Success "Docker containers started"
} catch {
    Print-Error "Failed to start Docker containers"
    Write-Host $_.Exception.Message
    exit 1
}

# Wait for PostgreSQL to be ready
Print-Status "Waiting for PostgreSQL to be ready..."
$maxAttempts = 30
$attempt = 0
$postgresReady = $false

while ($attempt -lt $maxAttempts) {
    try {
        & docker exec honua-postgres pg_isready -U honua -d honua 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Print-Success "PostgreSQL is ready"
            $postgresReady = $true
            break
        }
    } catch {}

    $attempt++
    Start-Sleep -Seconds 1
}

if (-not $postgresReady) {
    Print-Error "PostgreSQL failed to start after $maxAttempts seconds"
    exit 1
}

# Wait for Redis to be ready
Print-Status "Waiting for Redis to be ready..."
$attempt = 0
$redisReady = $false

while ($attempt -lt $maxAttempts) {
    try {
        $result = & docker exec honua-redis redis-cli ping 2>&1
        if ($result -eq "PONG") {
            Print-Success "Redis is ready"
            $redisReady = $true
            break
        }
    } catch {}

    $attempt++
    Start-Sleep -Seconds 1
}

if (-not $redisReady) {
    Print-Error "Redis failed to start after $maxAttempts seconds"
    exit 1
}

Write-Host ""

# Restore NuGet packages
Print-Status "Restoring NuGet packages..."
try {
    & dotnet restore
    if ($LASTEXITCODE -eq 0) {
        Print-Success "Packages restored"
    } else {
        throw "dotnet restore failed"
    }
} catch {
    Print-Error "Failed to restore packages"
    Write-Host $_.Exception.Message
    exit 1
}

Write-Host ""

# Build the solution
Print-Status "Building solution..."
try {
    & dotnet build --no-incremental
    if ($LASTEXITCODE -eq 0) {
        Print-Success "Build successful"
    } else {
        throw "dotnet build failed"
    }
} catch {
    Print-Error "Build failed"
    Write-Host $_.Exception.Message
    exit 1
}

Write-Host ""

# Run unit tests
Print-Status "Running unit tests..."
try {
    & dotnet test --filter "Category=Unit" --no-build --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Print-Success "Unit tests passed"
    } else {
        Print-Warning "Some unit tests failed (this may be expected on first run)"
    }
} catch {
    Print-Warning "Unit tests failed (this may be expected on first run)"
}

Write-Host ""
Write-Host "========================================"
Write-Host "  Setup Complete!"
Write-Host "========================================"
Write-Host ""
Write-Host "Your development environment is ready!"
Write-Host ""
Write-Host "Quick Start Commands:"
Write-Host "  • Start the server:     dotnet run --project src/Honua.Server.Host"
Write-Host "  • Run all tests:        dotnet test"
Write-Host "  • Run unit tests:       dotnet test --filter `"Category=Unit`""
Write-Host "  • Format code:          dotnet format"
Write-Host "  • Check coverage:       .\scripts\check-coverage.ps1"
Write-Host ""
Write-Host "Docker Services:"
Write-Host "  • PostgreSQL:           localhost:5432 (user: honua, password: honua_dev_password)"
Write-Host "  • Redis:                localhost:6379"
Write-Host ""
Write-Host "Useful Scripts:"
Write-Host "  • .\scripts\run-tests.ps1      - Run all tests with coverage"
Write-Host "  • .\scripts\format-code.ps1    - Format code"
Write-Host "  • .\scripts\reset-db.ps1       - Reset database"
Write-Host "  • .\scripts\seed-data.ps1      - Load test data"
Write-Host ""
Write-Host "API will be available at: http://localhost:8080"
Write-Host "Swagger UI:               http://localhost:8080/swagger"
Write-Host ""
Write-Host "For more information, see:"
Write-Host "  • CONTRIBUTING.md"
Write-Host "  • docs\development\quick-start.md"
Write-Host "  • docs\development\debugging.md"
Write-Host ""
