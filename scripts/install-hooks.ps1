#!/usr/bin/env pwsh
# Install Git pre-commit hooks for Honua Server (Windows PowerShell)

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

Print-Status "Installing Git pre-commit hooks..."

# Check if .git directory exists
if (-not (Test-Path ".git")) {
    Print-Warning "Not a git repository. Skipping hook installation."
    exit 0
}

# Create hooks directory if it doesn't exist
$hooksDir = Join-Path $ProjectRoot ".git\hooks"
if (-not (Test-Path $hooksDir)) {
    New-Item -ItemType Directory -Path $hooksDir | Out-Null
}

# Copy pre-commit hook
$sourceHook = Join-Path $ProjectRoot ".githooks\pre-commit"
$destHook = Join-Path $hooksDir "pre-commit"

if (Test-Path $sourceHook) {
    Copy-Item $sourceHook $destHook -Force
    Print-Success "Pre-commit hook installed"
} else {
    Print-Warning ".githooks\pre-commit not found"
}

Write-Host ""
Write-Host "Git hooks installed successfully!"
Write-Host ""
Write-Host "The pre-commit hook will automatically:"
Write-Host "  • Check code formatting (dotnet format)"
Write-Host "  • Build the project"
Write-Host "  • Run unit tests"
Write-Host ""
Write-Host "To bypass the hook (not recommended):"
Write-Host "  git commit --no-verify"
Write-Host ""
