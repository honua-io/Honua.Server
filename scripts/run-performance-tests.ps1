#!/usr/bin/env pwsh

param(
    [string]$Category = "",
    [string]$LoggerVerbosity = "detailed",
    [switch]$SkipBuild,
    [switch]$IncludeStress
)

$ErrorActionPreference = "Stop"

Write-Host "Running Honua Performance Tests..." -ForegroundColor Green

# Build the test filter
$testFilter = "TestCategory=Performance"

if ($Category) {
    $testFilter += " & TestCategory=$Category"
}

if (-not $IncludeStress) {
    $testFilter += " & TestCategory!=Stress"
    Write-Host "Excluding stress tests (use -IncludeStress to include)" -ForegroundColor Yellow
}

Write-Host "Test Filter: $testFilter" -ForegroundColor Blue

# Build arguments
$testArgs = @(
    "test",
    "HonuaCoreTest/Honua.PerformanceTests/Honua.PerformanceTests.csproj",
    "--configuration", "Release",
    "--logger", "console;verbosity=$LoggerVerbosity",
    "--logger", "trx;LogFileName=test-results/performance-test-results.trx",
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
        Write-Host "Performance tests completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Some performance tests failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "Error running performance tests: $_" -ForegroundColor Red
    exit 1
}

Write-Host "Test results saved to test-results/performance-test-results.trx" -ForegroundColor Blue