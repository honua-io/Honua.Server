#!/usr/bin/env pwsh
# Format C# code using dotnet format (Windows PowerShell)

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

Print-Status "Formatting C# code..."
Write-Host ""

# First check if there are any formatting issues
& dotnet format --verify-no-changes --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Print-Success "Code is already properly formatted!"
} else {
    Print-Warning "Code formatting issues found. Fixing..."
    Write-Host ""

    # Apply formatting
    & dotnet format

    Write-Host ""
    Print-Success "Code formatting applied!"
    Write-Host ""
    Write-Host "Files have been modified. Please review the changes and commit them."
}

Write-Host ""
