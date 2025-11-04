#!/usr/bin/env pwsh

param(
    [switch]$Fast,
    [switch]$ExcludeInfrastructure,
    [switch]$UnitOnly,
    [switch]$IntegrationOnly,
    [switch]$PerformanceOnly,
    [string]$LoggerVerbosity = "detailed",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "Honua Test Suite Runner" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

# Ensure we're in the right directory
if (-not (Test-Path "HonuaCore/Honua.Core.csproj")) {
    Write-Host "Error: Please run this script from the project root directory" -ForegroundColor Red
    exit 1
}

# Create test results directory
New-Item -ItemType Directory -Force -Path "test-results" | Out-Null

$totalTests = 0
$failedTests = 0
$results = @()

# Function to run tests and track results
function Invoke-TestSuite {
    param($Name, $ScriptPath, $Args)

    Write-Host "`nRunning $Name..." -ForegroundColor Green
    Write-Host "================================" -ForegroundColor Green

    try {
        $startTime = Get-Date
        & $ScriptPath @Args
        $endTime = Get-Date
        $duration = $endTime - $startTime

        if ($LASTEXITCODE -eq 0) {
            $results += [PSCustomObject]@{
                TestSuite = $Name
                Status = "PASSED"
                Duration = $duration.ToString("mm\:ss")
                ExitCode = $LASTEXITCODE
            }
            Write-Host "$Name completed successfully in $($duration.ToString('mm\:ss'))" -ForegroundColor Green
        } else {
            $script:failedTests++
            $results += [PSCustomObject]@{
                TestSuite = $Name
                Status = "FAILED"
                Duration = $duration.ToString("mm\:ss")
                ExitCode = $LASTEXITCODE
            }
            Write-Host "$Name failed with exit code $LASTEXITCODE after $($duration.ToString('mm\:ss'))" -ForegroundColor Red
        }
        $script:totalTests++
    } catch {
        $script:failedTests++
        $script:totalTests++
        $results += [PSCustomObject]@{
            TestSuite = $Name
            Status = "ERROR"
            Duration = "N/A"
            ExitCode = "Exception"
        }
        Write-Host "Error running $Name : $_" -ForegroundColor Red
    }
}

# Build solution first (unless skipping)
if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Blue
    dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Determine which test suites to run
$suites = @()

if ($UnitOnly) {
    $unitArgs = @{
        LoggerVerbosity = $LoggerVerbosity
        Fast = $Fast
        ExcludeInfrastructure = $ExcludeInfrastructure
    }
    $suites += @{ Name = "Unit Tests"; Script = "scripts/run-unit-tests.ps1"; Args = $unitArgs }
}
elseif ($IntegrationOnly) {
    $integrationArgs = @{
        LoggerVerbosity = $LoggerVerbosity
        SkipBuild = $true
    }
    $suites += @{ Name = "Integration Tests"; Script = "scripts/run-integration-tests.ps1"; Args = $integrationArgs }
}
elseif ($PerformanceOnly) {
    $performanceArgs = @{
        LoggerVerbosity = $LoggerVerbosity
        SkipBuild = $true
    }
    $suites += @{ Name = "Performance Tests"; Script = "scripts/run-performance-tests.ps1"; Args = $performanceArgs }
}
else {
    # Run all test suites
    $unitArgs = @{
        LoggerVerbosity = $LoggerVerbosity
        Fast = $Fast
        ExcludeInfrastructure = $ExcludeInfrastructure
    }

    $integrationArgs = @{
        LoggerVerbosity = $LoggerVerbosity
        SkipBuild = $true
    }

    $performanceArgs = @{
        LoggerVerbosity = $LoggerVerbosity
        SkipBuild = $true
    }

    $suites += @{ Name = "Unit Tests"; Script = "scripts/run-unit-tests.ps1"; Args = $unitArgs }
    $suites += @{ Name = "Integration Tests"; Script = "scripts/run-integration-tests.ps1"; Args = $integrationArgs }

    if (-not $Fast) {
        $suites += @{ Name = "Performance Tests"; Script = "scripts/run-performance-tests.ps1"; Args = $performanceArgs }
    }
}

# Run test suites
foreach ($suite in $suites) {
    Invoke-TestSuite -Name $suite.Name -ScriptPath $suite.Script -Args $suite.Args
}

# Display summary
Write-Host "`n" -ForegroundColor White
Write-Host "TEST EXECUTION SUMMARY" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

$results | Format-Table -AutoSize

$passedTests = $totalTests - $failedTests
Write-Host "`nTotal Test Suites: $totalTests" -ForegroundColor White
Write-Host "Passed: $passedTests" -ForegroundColor Green
Write-Host "Failed: $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Green" })

if ($failedTests -gt 0) {
    Write-Host "`nSome test suites failed. Check individual test results for details." -ForegroundColor Red
    Write-Host "Test results are available in the test-results/ directory." -ForegroundColor Blue
    exit 1
} else {
    Write-Host "`nAll test suites passed successfully!" -ForegroundColor Green
    Write-Host "Test results are available in the test-results/ directory." -ForegroundColor Blue
    exit 0
}