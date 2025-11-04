#!/usr/bin/env bash
#
# verify-test-setup.sh
# Verifies that the parallel testing infrastructure is properly set up
#
# Usage:
#   ./scripts/verify-test-setup.sh

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Verifying Parallel Test Infrastructure                   ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

ERRORS=0
WARNINGS=0

# Check: Docker
echo -e "${YELLOW}Checking Docker...${NC}"
if command -v docker &> /dev/null; then
    DOCKER_VERSION=$(docker --version | cut -d' ' -f3 | cut -d',' -f1)
    echo -e "  ${GREEN}✓ Docker installed: $DOCKER_VERSION${NC}"
else
    echo -e "  ${RED}✗ Docker not found${NC}"
    ERRORS=$((ERRORS + 1))
fi

# Check: Docker Compose
echo -e "${YELLOW}Checking Docker Compose...${NC}"
if command -v docker-compose &> /dev/null || docker compose version &> /dev/null; then
    COMPOSE_VERSION=$(docker-compose --version 2>/dev/null | cut -d' ' -f4 || docker compose version | head -1 | cut -d' ' -f4)
    echo -e "  ${GREEN}✓ Docker Compose installed: $COMPOSE_VERSION${NC}"
else
    echo -e "  ${RED}✗ Docker Compose not found${NC}"
    ERRORS=$((ERRORS + 1))
fi

# Check: .NET SDK
echo -e "${YELLOW}Checking .NET SDK...${NC}"
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo -e "  ${GREEN}✓ .NET SDK installed: $DOTNET_VERSION${NC}"

    if [[ "$DOTNET_VERSION" < "9.0" ]]; then
        echo -e "  ${YELLOW}⚠ .NET 9.0 or higher recommended${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
else
    echo -e "  ${RED}✗ .NET SDK not found${NC}"
    ERRORS=$((ERRORS + 1))
fi

# Check: Python 3
echo -e "${YELLOW}Checking Python...${NC}"
if command -v python3 &> /dev/null; then
    PYTHON_VERSION=$(python3 --version | cut -d' ' -f2)
    echo -e "  ${GREEN}✓ Python 3 installed: $PYTHON_VERSION${NC}"
else
    echo -e "  ${RED}✗ Python 3 not found${NC}"
    ERRORS=$((ERRORS + 1))
fi

# Check: pytest
echo -e "${YELLOW}Checking pytest...${NC}"
if python3 -c "import pytest" 2>/dev/null; then
    PYTEST_VERSION=$(python3 -c "import pytest; print(pytest.__version__)")
    echo -e "  ${GREEN}✓ pytest installed: $PYTEST_VERSION${NC}"
else
    echo -e "  ${YELLOW}⚠ pytest not found (will be installed in venv)${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# Check: pytest-xdist
echo -e "${YELLOW}Checking pytest-xdist...${NC}"
if python3 -c "import xdist" 2>/dev/null; then
    echo -e "  ${GREEN}✓ pytest-xdist installed${NC}"
else
    echo -e "  ${YELLOW}⚠ pytest-xdist not found (will be installed in venv)${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# Check: QGIS (optional)
echo -e "${YELLOW}Checking QGIS...${NC}"
if python3 -c "from qgis.core import QgsApplication" 2>/dev/null; then
    echo -e "  ${GREEN}✓ QGIS Python bindings found${NC}"
else
    echo -e "  ${YELLOW}⚠ QGIS not found (required for QGIS tests only)${NC}"
    echo -e "    Install: sudo apt-get install qgis python3-qgis"
    WARNINGS=$((WARNINGS + 1))
fi

# Check: CPU count
echo -e "${YELLOW}Checking CPU cores...${NC}"
CPU_COUNT=$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo "unknown")
if [[ "$CPU_COUNT" != "unknown" ]]; then
    echo -e "  ${GREEN}✓ CPU cores: $CPU_COUNT${NC}"

    if [[ "$CPU_COUNT" -lt 8 ]]; then
        echo -e "  ${YELLOW}⚠ Less than 8 cores detected. Parallel testing may be slower.${NC}"
        echo -e "    Consider using --sequential mode or reducing worker counts."
        WARNINGS=$((WARNINGS + 1))
    fi
else
    echo -e "  ${YELLOW}⚠ Unable to detect CPU count${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# Check: Memory
echo -e "${YELLOW}Checking memory...${NC}"
if command -v free &> /dev/null; then
    TOTAL_MEM_GB=$(free -g | awk '/^Mem:/{print $2}')
    echo -e "  ${GREEN}✓ Total memory: ${TOTAL_MEM_GB}GB${NC}"

    if [[ "$TOTAL_MEM_GB" -lt 8 ]]; then
        echo -e "  ${YELLOW}⚠ Less than 8GB memory detected${NC}"
        echo -e "    Parallel tests may use significant memory (up to 8GB)"
        WARNINGS=$((WARNINGS + 1))
    fi
elif command -v sysctl &> /dev/null; then
    TOTAL_MEM_BYTES=$(sysctl -n hw.memsize 2>/dev/null || echo "0")
    if [[ "$TOTAL_MEM_BYTES" != "0" ]]; then
        TOTAL_MEM_GB=$((TOTAL_MEM_BYTES / 1024 / 1024 / 1024))
        echo -e "  ${GREEN}✓ Total memory: ${TOTAL_MEM_GB}GB${NC}"
    fi
else
    echo -e "  ${YELLOW}⚠ Unable to detect memory${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# Check: Test data files
echo -e "${YELLOW}Checking test data...${NC}"
TEST_DATA_FILES=(
    "tests/TestData/test-metadata.json"
    "tests/TestData/ogc-sample.db"
)

for file in "${TEST_DATA_FILES[@]}"; do
    if [[ -f "$PROJECT_ROOT/$file" ]]; then
        echo -e "  ${GREEN}✓ Found: $file${NC}"
    else
        echo -e "  ${RED}✗ Missing: $file${NC}"
        ERRORS=$((ERRORS + 1))
    fi
done

# Check: Scripts executable
echo -e "${YELLOW}Checking scripts...${NC}"
SCRIPTS=(
    "scripts/build-test-cache.sh"
    "scripts/run-tests-parallel.sh"
    "scripts/run-tests-csharp-parallel.sh"
    "scripts/run-tests-python-parallel.sh"
    "scripts/run-tests-qgis-parallel.sh"
)

for script in "${SCRIPTS[@]}"; do
    if [[ -x "$PROJECT_ROOT/$script" ]]; then
        echo -e "  ${GREEN}✓ Executable: $script${NC}"
    elif [[ -f "$PROJECT_ROOT/$script" ]]; then
        echo -e "  ${YELLOW}⚠ Not executable: $script${NC}"
        echo -e "    Run: chmod +x $script"
        WARNINGS=$((WARNINGS + 1))
    else
        echo -e "  ${RED}✗ Missing: $script${NC}"
        ERRORS=$((ERRORS + 1))
    fi
done

# Check: xUnit configuration
echo -e "${YELLOW}Checking xUnit configuration...${NC}"
if [[ -f "$PROJECT_ROOT/tests/xunit.runner.json" ]]; then
    echo -e "  ${GREEN}✓ Found: tests/xunit.runner.json${NC}"

    MAX_THREADS=$(jq -r '.maxParallelThreads // "unknown"' "$PROJECT_ROOT/tests/xunit.runner.json" 2>/dev/null || echo "unknown")
    if [[ "$MAX_THREADS" != "unknown" ]]; then
        echo -e "    maxParallelThreads: $MAX_THREADS"
    fi
else
    echo -e "  ${YELLOW}⚠ Missing: tests/xunit.runner.json${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# Check: Docker daemon
echo -e "${YELLOW}Checking Docker daemon...${NC}"
if docker ps &> /dev/null; then
    echo -e "  ${GREEN}✓ Docker daemon is running${NC}"
else
    echo -e "  ${RED}✗ Docker daemon not running${NC}"
    echo -e "    Start Docker daemon and try again"
    ERRORS=$((ERRORS + 1))
fi

# Check: Disk space
echo -e "${YELLOW}Checking disk space...${NC}"
AVAIL_GB=$(df -BG "$PROJECT_ROOT" | tail -1 | awk '{print $4}' | tr -d 'G')
if [[ "$AVAIL_GB" -gt 10 ]]; then
    echo -e "  ${GREEN}✓ Available disk space: ${AVAIL_GB}GB${NC}"
elif [[ "$AVAIL_GB" -gt 5 ]]; then
    echo -e "  ${YELLOW}⚠ Low disk space: ${AVAIL_GB}GB${NC}"
    echo -e "    Recommend at least 10GB free for test results and Docker images"
    WARNINGS=$((WARNINGS + 1))
else
    echo -e "  ${RED}✗ Very low disk space: ${AVAIL_GB}GB${NC}"
    echo -e "    At least 5GB required for testing"
    ERRORS=$((ERRORS + 1))
fi

# Summary
echo ""
echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Verification Summary                                      ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

if [[ $ERRORS -gt 0 ]]; then
    echo -e "${RED}✗ Found $ERRORS error(s)${NC}"
    echo -e "  Fix the errors above before running parallel tests"
    EXIT_CODE=1
elif [[ $WARNINGS -gt 0 ]]; then
    echo -e "${YELLOW}⚠ Found $WARNINGS warning(s)${NC}"
    echo -e "  You can proceed, but consider addressing warnings for optimal performance"
    EXIT_CODE=0
else
    echo -e "${GREEN}✓ All checks passed!${NC}"
    echo -e "  Your system is ready for parallel testing"
    EXIT_CODE=0
fi

echo ""

if [[ $EXIT_CODE -eq 0 ]]; then
    echo -e "${GREEN}Next steps:${NC}"
    echo -e "  1. Build test cache:"
    echo -e "     ${BLUE}./scripts/build-test-cache.sh${NC}"
    echo -e ""
    echo -e "  2. Run all tests in parallel:"
    echo -e "     ${BLUE}./scripts/run-tests-parallel.sh${NC}"
    echo -e ""
    echo -e "  3. Or run specific suites:"
    echo -e "     ${BLUE}./scripts/run-tests-csharp-parallel.sh${NC}"
    echo -e "     ${BLUE}./scripts/run-tests-python-parallel.sh${NC}"
    echo -e "     ${BLUE}./scripts/run-tests-qgis-parallel.sh${NC}"
    echo ""
fi

exit $EXIT_CODE
