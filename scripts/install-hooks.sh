#!/bin/bash
# Install Git pre-commit hooks for Honua Server

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get the script directory and project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

echo -e "${BLUE}==>${NC} Installing Git pre-commit hooks..."

# Check if .git directory exists
if [ ! -d ".git" ]; then
    echo -e "${YELLOW}!${NC} Not a git repository. Skipping hook installation."
    exit 0
fi

# Create hooks directory if it doesn't exist
mkdir -p .git/hooks

# Copy pre-commit hook
if [ -f ".githooks/pre-commit" ]; then
    cp .githooks/pre-commit .git/hooks/pre-commit
    chmod +x .git/hooks/pre-commit
    echo -e "${GREEN}✓${NC} Pre-commit hook installed"
else
    echo -e "${YELLOW}!${NC} .githooks/pre-commit not found"
fi

echo ""
echo "Git hooks installed successfully!"
echo ""
echo "The pre-commit hook will automatically:"
echo "  • Check code formatting (dotnet format)"
echo "  • Build the project"
echo "  • Run unit tests"
echo ""
echo "To bypass the hook (not recommended):"
echo "  git commit --no-verify"
echo ""
