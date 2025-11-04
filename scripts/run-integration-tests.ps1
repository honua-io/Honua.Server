#!/usr/bin/env pwsh

param(
    [string]$Category = "",
    [string]$LoggerVerbosity = "detailed",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "Running Honua Integration Tests..." -ForegroundColor Green

# Build the test filter
$testFilter = "TestCategory=Integration"

if ($Category) {
    $testFilter += " & TestCategory=$Category"
}

Write-Host "Test Filter: $testFilter" -ForegroundColor Blue

# Build arguments
$testArgs = @(
    "test",
    "HonuaCoreTest/Honua.IntegrationTests/Honua.IntegrationTests.csproj",
    "--configuration", "Release",
    "--logger", "console;verbosity=$LoggerVerbosity",
    "--logger", "trx;LogFileName=test-results/integration-test-results.trx",
    "--settings", "test.runsettings",
    "--filter", $testFilter
)

if ($SkipBuild) {
    $testArgs += "--no-build"
}

# Create test results directory
New-Item -ItemType Directory -Force -Path "test-results" | Out-Null

Write-Host "Executing: dotnet $($testArgs -join ' ')" -ForegroundColor Blue

try {
    & dotnet @testArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Integration tests completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Some integration tests failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "Error running integration tests: $_" -ForegroundColor Red
    exit 1
}

Write-Host "Test results saved to test-results/integration-test-results.trx" -ForegroundColor Blue