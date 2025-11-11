#!/usr/bin/env pwsh
# Run all tests with code coverage for Honua Server (Windows PowerShell)

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

# Get project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Print-Status "Running all tests with code coverage..."
Write-Host ""

# Run tests with coverage
& dotnet test `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=opencover `
    /p:CoverletOutput=./TestResults/ `
    /p:ExcludeByFile="**/Migrations/**" `
    /p:Exclude="[*.Tests]*"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Print-Success "Tests completed!"
    Write-Host ""
    Write-Host "To view detailed coverage report:"
    Write-Host "  • Install reportgenerator: dotnet tool install -g dotnet-reportgenerator-globaltool"
    Write-Host "  • Generate report: reportgenerator -reports:`"**/TestResults/coverage.opencover.xml`" -targetdir:TestResults\Report"
    Write-Host "  • Open: TestResults\Report\index.html"
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "Some tests failed. Please review the output above." -ForegroundColor Red
    exit 1
}
