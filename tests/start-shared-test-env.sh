#!/bin/bash
# Start Shared Test Environment
# ==============================
# This script manages the shared Docker test environment for Honua Server.
# It can start, stop, restart, and check the health of the test infrastructure.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.shared-test-env.yml"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
print_header() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "  $1"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

check_docker() {
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed or not in PATH"
        exit 1
    fi

    if ! docker info &> /dev/null; then
        print_error "Docker daemon is not running"
        exit 1
    fi

    print_success "Docker is available"
}

start_env() {
    print_header "Starting Shared Test Environment"

    check_docker

    # Create cache directory if it doesn't exist
    mkdir -p "$SCRIPT_DIR/shared-cache"

    # Start services
    echo "Starting Docker services..."
    docker-compose -f "$COMPOSE_FILE" up -d

    echo ""
    echo "Waiting for services to be healthy..."

    # Wait for Honua server
    echo -n "  Honua Server (port 5100): "
    for i in {1..30}; do
        if curl -sf http://localhost:5100/health > /dev/null 2>&1; then
            print_success "Ready"
            break
        fi
        if [ $i -eq 30 ]; then
            print_error "Failed to start (timeout)"
            exit 1
        fi
        sleep 1
    done

    # Wait for PostgreSQL
    echo -n "  PostgreSQL (port 5433): "
    for i in {1..30}; do
        if docker exec postgres-test-shared pg_isready -U postgres > /dev/null 2>&1; then
            print_success "Ready"
            break
        fi
        if [ $i -eq 30 ]; then
            print_error "Failed to start (timeout)"
            exit 1
        fi
        sleep 1
    done

    # Wait for Redis
    echo -n "  Redis (port 6380): "
    for i in {1..20}; do
        if docker exec redis-test-shared redis-cli ping > /dev/null 2>&1; then
            print_success "Ready"
            break
        fi
        if [ $i -eq 20 ]; then
            print_warning "Failed to start"
        fi
        sleep 1
    done

    # Wait for Qdrant
    echo -n "  Qdrant (port 6334): "
    for i in {1..20}; do
        if curl -sf http://localhost:6334/health > /dev/null 2>&1; then
            print_success "Ready"
            break
        fi
        if [ $i -eq 20 ]; then
            print_warning "Failed to start"
        fi
        sleep 1
    done

    echo ""
    print_success "Shared test environment is ready!"
    echo ""
    echo "Services available at:"
    echo "  • Honua Server:  http://localhost:5100"
    echo "  • PostgreSQL:    localhost:5433 (user: postgres, password: test, db: honua_test)"
    echo "  • Redis:         localhost:6380"
    echo "  • Qdrant:        http://localhost:6334"
    echo ""
    echo "Environment variables for tests:"
    echo "  export HONUA_API_BASE_URL=http://localhost:5100"
    echo "  export POSTGRES_TEST_CONNECTION='Host=localhost;Port=5433;Database=honua_test;Username=postgres;Password=test'"
    echo "  export REDIS_TEST_CONNECTION='localhost:6380'"
    echo "  export QDRANT_TEST_URL='http://localhost:6334'"
    echo ""
}

stop_env() {
    print_header "Stopping Shared Test Environment"

    docker-compose -f "$COMPOSE_FILE" down

    print_success "Shared test environment stopped"
}

restart_env() {
    print_header "Restarting Shared Test Environment"

    docker-compose -f "$COMPOSE_FILE" restart

    print_success "Shared test environment restarted"
}

status_env() {
    print_header "Shared Test Environment Status"

    docker-compose -f "$COMPOSE_FILE" ps
}

logs_env() {
    SERVICE="${1:-honua-test}"
    docker-compose -f "$COMPOSE_FILE" logs -f "$SERVICE"
}

clean_env() {
    print_header "Cleaning Shared Test Environment"

    print_warning "This will remove all containers, networks, and volumes"
    read -p "Are you sure? (y/N) " -n 1 -r
    echo

    if [[ $REPLY =~ ^[Yy]$ ]]; then
        docker-compose -f "$COMPOSE_FILE" down -v
        rm -rf "$SCRIPT_DIR/shared-cache"
        print_success "Shared test environment cleaned"
    else
        echo "Cancelled"
    fi
}

usage() {
    cat << EOF
Honua Shared Test Environment Manager

Usage: $0 <command>

Commands:
    start       Start the shared test environment
    stop        Stop the shared test environment
    restart     Restart all services
    status      Show status of all services
    logs        Show logs (default: honua-test, or specify service name)
    clean       Stop and remove all containers, networks, and volumes
    help        Show this help message

Examples:
    $0 start                 # Start all test services
    $0 logs honua-test       # Tail logs from Honua server
    $0 logs postgres-test    # Tail logs from PostgreSQL
    $0 stop                  # Stop all services

Environment Variables (set after starting):
    HONUA_API_BASE_URL       Base URL for Honua API tests
    POSTGRES_TEST_CONNECTION Connection string for PostgreSQL tests
    REDIS_TEST_CONNECTION    Connection string for Redis tests
    QDRANT_TEST_URL          URL for Qdrant tests

EOF
}

# Main
case "${1:-}" in
    start)
        start_env
        ;;
    stop)
        stop_env
        ;;
    restart)
        restart_env
        ;;
    status)
        status_env
        ;;
    logs)
        logs_env "${2:-honua-test}"
        ;;
    clean)
        clean_env
        ;;
    help|--help|-h)
        usage
        ;;
    *)
        echo "Unknown command: ${1:-}"
        echo ""
        usage
        exit 1
        ;;
esac
