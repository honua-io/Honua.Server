#!/bin/bash
# Development Environment Setup Script for Honua Server (Linux/macOS)
# This script sets up your local development environment with all required dependencies

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Print colored output
print_status() {
    echo -e "${BLUE}==>${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}!${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

# Get the script directory and project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

echo ""
echo "========================================"
echo "  Honua Server Development Setup"
echo "========================================"
echo ""

# Check for required dependencies
print_status "Checking for required dependencies..."

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK not found!"
    echo "Please install .NET 9 SDK from: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
else
    DOTNET_VERSION=$(dotnet --version)
    print_success ".NET SDK found: $DOTNET_VERSION"
fi

# Check Docker
if ! command -v docker &> /dev/null; then
    print_error "Docker not found!"
    echo "Please install Docker Desktop from: https://www.docker.com/products/docker-desktop"
    exit 1
else
    DOCKER_VERSION=$(docker --version | cut -d ' ' -f3 | cut -d ',' -f1)
    print_success "Docker found: $DOCKER_VERSION"
fi

# Check Docker Compose
if ! command -v docker compose &> /dev/null && ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose not found!"
    echo "Please install Docker Compose"
    exit 1
else
    print_success "Docker Compose found"
fi

# Check Git
if ! command -v git &> /dev/null; then
    print_error "Git not found!"
    echo "Please install Git from: https://git-scm.com/downloads"
    exit 1
else
    GIT_VERSION=$(git --version | cut -d ' ' -f3)
    print_success "Git found: $GIT_VERSION"
fi

echo ""

# Install Git hooks
print_status "Installing Git pre-commit hooks..."
if [ -f "$SCRIPT_DIR/install-hooks.sh" ]; then
    bash "$SCRIPT_DIR/install-hooks.sh"
    print_success "Git hooks installed"
else
    print_warning "install-hooks.sh not found, skipping Git hooks installation"
fi

echo ""

# Start Docker containers
print_status "Starting Docker containers (PostgreSQL, Redis)..."
if docker compose up -d postgres redis; then
    print_success "Docker containers started"
else
    print_error "Failed to start Docker containers"
    exit 1
fi

# Wait for PostgreSQL to be ready
print_status "Waiting for PostgreSQL to be ready..."
for i in {1..30}; do
    if docker exec honua-postgres pg_isready -U honua -d honua > /dev/null 2>&1; then
        print_success "PostgreSQL is ready"
        break
    fi
    if [ $i -eq 30 ]; then
        print_error "PostgreSQL failed to start after 30 seconds"
        exit 1
    fi
    sleep 1
done

# Wait for Redis to be ready
print_status "Waiting for Redis to be ready..."
for i in {1..30}; do
    if docker exec honua-redis redis-cli ping > /dev/null 2>&1; then
        print_success "Redis is ready"
        break
    fi
    if [ $i -eq 30 ]; then
        print_error "Redis failed to start after 30 seconds"
        exit 1
    fi
    sleep 1
done

echo ""

# Restore NuGet packages
print_status "Restoring NuGet packages..."
if dotnet restore; then
    print_success "Packages restored"
else
    print_error "Failed to restore packages"
    exit 1
fi

echo ""

# Build the solution
print_status "Building solution..."
if dotnet build --no-incremental; then
    print_success "Build successful"
else
    print_error "Build failed"
    exit 1
fi

echo ""

# Run unit tests
print_status "Running unit tests..."
if dotnet test --filter "Category=Unit" --no-build --verbosity quiet; then
    print_success "Unit tests passed"
else
    print_warning "Some unit tests failed (this may be expected on first run)"
fi

echo ""
echo "========================================"
echo "  Setup Complete!"
echo "========================================"
echo ""
echo "Your development environment is ready!"
echo ""
echo "Quick Start Commands:"
echo "  • Start the server:     dotnet run --project src/Honua.Server.Host"
echo "  • Run all tests:        dotnet test"
echo "  • Run unit tests:       dotnet test --filter \"Category=Unit\""
echo "  • Format code:          dotnet format"
echo "  • Check coverage:       ./scripts/check-coverage.sh"
echo ""
echo "Docker Services:"
echo "  • PostgreSQL:           localhost:5432 (user: honua, password: honua_dev_password)"
echo "  • Redis:                localhost:6379"
echo ""
echo "Useful Scripts:"
echo "  • ./scripts/run-tests.sh      - Run all tests with coverage"
echo "  • ./scripts/format-code.sh    - Format code"
echo "  • ./scripts/reset-db.sh       - Reset database"
echo "  • ./scripts/seed-data.sh      - Load test data"
echo ""
echo "API will be available at: http://localhost:8080"
echo "Swagger UI:               http://localhost:8080/swagger"
echo ""
echo "For more information, see:"
echo "  • CONTRIBUTING.md"
echo "  • docs/development/quick-start.md"
echo "  • docs/development/debugging.md"
echo ""
