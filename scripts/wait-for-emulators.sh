#!/usr/bin/env bash
#
# wait-for-emulators.sh - Wait for cloud storage emulators to be healthy
#
# This script waits for LocalStack (S3), Azurite (Azure Blob), and GCS emulator
# to be fully ready before running integration tests.
#
# Usage:
#   ./scripts/wait-for-emulators.sh [timeout_seconds]
#
# Default timeout: 120 seconds

set -euo pipefail

# Configuration
TIMEOUT="${1:-120}"
LOCALSTACK_URL="${LOCALSTACK_URL:-http://localhost:4566}"
AZURITE_URL="${AZURITE_URL:-http://localhost:10000}"
GCS_URL="${GCS_URL:-http://localhost:4443}"
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-15432}"
POSTGRES_USER="${POSTGRES_USER:-honua}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $*"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $*"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $*"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $*"
}

# Check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check LocalStack health
check_localstack() {
    if curl -sf "${LOCALSTACK_URL}/_localstack/health" | grep -q '"s3".*"running"'; then
        return 0
    else
        return 1
    fi
}

# Check Azurite health
check_azurite() {
    if curl -sf "${AZURITE_URL}/devstoreaccount1?comp=list" >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Check GCS emulator health
check_gcs() {
    if curl -sf "${GCS_URL}/storage/v1/b" >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Check Postgres/PostGIS health
check_postgres() {
    if command_exists pg_isready; then
        if pg_isready -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" >/dev/null 2>&1; then
            return 0
        fi
    fi

    if (exec 3<>"/dev/tcp/${POSTGRES_HOST}/${POSTGRES_PORT}") >/dev/null 2>&1; then
        exec 3>&-
        return 0
    fi

    return 1
}

# Wait for a service with timeout
wait_for_service() {
    local service_name="$1"
    local check_function="$2"
    local start_time=$(date +%s)
    local elapsed=0

    log_info "Waiting for ${service_name} to be ready (timeout: ${TIMEOUT}s)..."

    while [ $elapsed -lt $TIMEOUT ]; do
        if $check_function; then
            log_success "${service_name} is ready!"
            return 0
        fi

        sleep 2
        elapsed=$(( $(date +%s) - start_time ))

        # Show progress every 10 seconds
        if [ $(( elapsed % 10 )) -eq 0 ] && [ $elapsed -gt 0 ]; then
            log_info "${service_name} not ready yet... (${elapsed}s elapsed)"
        fi
    done

    log_error "${service_name} did not become ready within ${TIMEOUT} seconds"
    return 1
}

# Main execution
main() {
    log_info "Cloud Storage Emulator Health Check"
    log_info "===================================="
    log_info ""

    # Check required tools
    if ! command_exists curl; then
        log_error "curl is required but not installed"
        exit 1
    fi

    # Wait for each emulator
    local all_healthy=true

    if ! wait_for_service "LocalStack (S3)" check_localstack; then
        all_healthy=false
    fi

    if ! wait_for_service "Azurite (Azure Blob)" check_azurite; then
        all_healthy=false
    fi

    if ! wait_for_service "GCS Emulator" check_gcs; then
        all_healthy=false
    fi

    if ! wait_for_service "PostGIS Emulator" check_postgres; then
        all_healthy=false
    fi

    # Final summary
    log_info ""
    log_info "Health Check Summary"
    log_info "===================="

    if check_localstack; then
        log_success "LocalStack (S3): ${LOCALSTACK_URL}"
        if command_exists jq; then
            curl -s "${LOCALSTACK_URL}/_localstack/health" | jq -r '.services.s3 // "status unknown"' | sed 's/^/  /'
        fi
    else
        log_error "LocalStack (S3): Not healthy"
    fi

    if check_azurite; then
        log_success "Azurite (Azure Blob): ${AZURITE_URL}"
    else
        log_error "Azurite (Azure Blob): Not healthy"
    fi

    if check_gcs; then
        log_success "GCS Emulator: ${GCS_URL}"
        if command_exists jq; then
            curl -s "${GCS_URL}/storage/v1/b" | jq -r '.kind // "status unknown"' | sed 's/^/  /'
        fi
    else
        log_error "GCS Emulator: Not healthy"
    fi

    if check_postgres; then
        log_success "PostGIS Emulator: ${POSTGRES_HOST}:${POSTGRES_PORT}"
    else
        log_error "PostGIS Emulator: Not healthy"
    fi

    log_info ""

    if [ "$all_healthy" = true ]; then
        log_success "All emulators are healthy and ready for testing!"
        return 0
    else
        log_error "Some emulators failed to become healthy"
        log_info ""
        log_info "Troubleshooting steps:"
        log_info "  1. Check if Docker is running: docker ps"
        log_info "  2. Check emulator logs: cd tests/Honua.Server.Core.Tests && docker-compose -f docker-compose.storage-emulators.yml logs"
        log_info "  3. Restart emulators: cd tests/Honua.Server.Core.Tests && docker-compose -f docker-compose.storage-emulators.yml restart"
        return 1
    fi
}

# Run main function
main "$@"
