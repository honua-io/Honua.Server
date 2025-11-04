#!/usr/bin/env pwsh

param(
    [switch]$ExcludeInfrastructure,
    [switch]$Fast,
    [string]$Category = "",
    [string]$LoggerVerbosity = "detailed"
)

$ErrorActionPreference = "Stop"

Write-Host "Running Honua Unit Tests..." -ForegroundColor Green

# Build the test filter
$testFilter = "TestCategory=Unit"

if ($Category) {
    $testFilter += " & TestCategory=$Category"
}

if ($ExcludeInfrastructure) {
    $testFilter += " & TestCategory!=Infrastructure"
    Write-Host "Excluding infrastructure-dependent tests" -ForegroundColor Yellow
}

if ($Fast) {
    $testFilter += " & TestCategory!=LongRunning & TestCategory!=Stress"
    Write-Host "Running fast tests only (excluding LongRunning and Stress)" -ForegroundColor Yellow
}

Write-Host "Test Filter: $testFilter" -ForegroundColor Blue

# Run the tests
$testArgs = @(
    "test",
    "HonuaCoreTest/Honua.UnitTests/Honua.UnitTests.csproj",
    "--configuration", "Release",
    "--logger", "console;verbosity=$LoggerVerbosity",
    "--logger", "trx;LogFileName=test-results/unit-test-results.trx",
    "--settings", "test.runsettings",
    "--filter", $testFilter
)

# Create test results directory
New-Item -ItemType Directory -Force -Path "test-results" | Out-Null

Write-Host "Executing: dotnet $($testArgs -join ' ')" -ForegroundColor Blue

try {
    & dotnet @testArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Unit tests completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Some unit tests failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "Error running unit tests: $_" -ForegroundColor Red
    exit 1
}

Write-Host "Test results saved to test-results/unit-test-results.trx" -ForegroundColor Blue