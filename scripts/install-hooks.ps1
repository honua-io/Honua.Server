# Install git hooks for Honua.Server

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$HooksDir = Join-Path $RepoRoot ".githooks"

Write-Host "Installing git hooks..."

# Configure git to use .githooks directory
git config core.hooksPath $HooksDir

Write-Host "✅ Git hooks installed successfully!" -ForegroundColor Green
Write-Host "Pre-commit hook will run on every commit."
Write-Host ""
Write-Host "To skip hooks (not recommended), use: git commit --no-verify"
