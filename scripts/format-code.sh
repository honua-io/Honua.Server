#!/bin/bash
# Format C# code using dotnet format

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

echo -e "${BLUE}==>${NC} Formatting C# code..."
echo ""

# First check if there are any formatting issues
if dotnet format --verify-no-changes --verbosity quiet; then
    echo -e "${GREEN}✓${NC} Code is already properly formatted!"
else
    echo -e "${YELLOW}!${NC} Code formatting issues found. Fixing..."
    echo ""

    # Apply formatting
    dotnet format

    echo ""
    echo -e "${GREEN}✓${NC} Code formatting applied!"
    echo ""
    echo "Files have been modified. Please review the changes and commit them."
fi

echo ""
