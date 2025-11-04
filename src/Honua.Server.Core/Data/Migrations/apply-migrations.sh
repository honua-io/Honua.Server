#!/bin/bash
# ============================================================================
# Honua Build Orchestrator - Migration Application Script
# ============================================================================
# Description: Applies database migrations to PostgreSQL
# Usage: ./apply-migrations.sh [OPTIONS]
#
# Options:
#   -h, --host HOST          PostgreSQL host (default: localhost)
#   -p, --port PORT          PostgreSQL port (default: 5432)
#   -d, --database DATABASE  Database name (default: honua)
#   -u, --user USER          Database user (default: postgres)
#   -v, --verify-only        Verify migrations without applying
#   --help                   Show this help message
# ============================================================================

set -euo pipefail

# Default values
PG_HOST="${PG_HOST:-localhost}"
PG_PORT="${PG_PORT:-5432}"
PG_DATABASE="${PG_DATABASE:-honua}"
PG_USER="${PG_USER:-postgres}"
VERIFY_ONLY=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ============================================================================
# Functions
# ============================================================================

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

show_help() {
    head -n 20 "$0" | grep "^#" | sed 's/^# //' | sed 's/^#//'
    exit 0
}

check_psql() {
    if ! command -v psql &> /dev/null; then
        print_error "psql command not found. Please install PostgreSQL client."
        exit 1
    fi
}

test_connection() {
    print_info "Testing database connection..."
    if psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -c "SELECT version();" &> /dev/null; then
        print_success "Database connection successful"
        return 0
    else
        print_error "Failed to connect to database"
        print_error "Host: $PG_HOST:$PG_PORT, Database: $PG_DATABASE, User: $PG_USER"
        return 1
    fi
}

get_current_version() {
    local version=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -tAc \
        "SELECT version FROM schema_migrations ORDER BY version DESC LIMIT 1;" 2>/dev/null || echo "")

    if [ -z "$version" ]; then
        echo "None"
    else
        echo "$version"
    fi
}

check_migrations_table() {
    local exists=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -tAc \
        "SELECT EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'schema_migrations'
        );" 2>/dev/null || echo "f")

    if [ "$exists" = "t" ]; then
        return 0
    else
        return 1
    fi
}

list_applied_migrations() {
    print_info "Applied migrations:"
    if check_migrations_table; then
        psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -c \
            "SELECT version, name, applied_at, execution_time_ms || 'ms' as duration
             FROM schema_migrations
             ORDER BY version;"
    else
        print_warning "No migrations applied yet (schema_migrations table does not exist)"
    fi
}

apply_migration() {
    local migration_file="$1"
    local version=$(basename "$migration_file" | cut -d'_' -f1)
    local name=$(basename "$migration_file" | sed 's/^[0-9]*_//' | sed 's/.sql$//')

    print_info "Applying migration $version: $name"

    local start_time=$(date +%s%3N)

    if psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -f "$migration_file" &> /tmp/migration_output.log; then
        local end_time=$(date +%s%3N)
        local duration=$((end_time - start_time))

        print_success "Migration $version applied successfully in ${duration}ms"
        return 0
    else
        print_error "Migration $version failed!"
        print_error "Output:"
        cat /tmp/migration_output.log
        return 1
    fi
}

verify_migrations() {
    print_info "Verifying migration files..."

    local migration_files=("$SCRIPT_DIR"/[0-9][0-9][0-9]_*.sql)

    if [ ! -f "${migration_files[0]}" ]; then
        print_error "No migration files found in $SCRIPT_DIR"
        exit 1
    fi

    print_info "Found ${#migration_files[@]} migration files:"
    for file in "${migration_files[@]}"; do
        local version=$(basename "$file" | cut -d'_' -f1)
        local name=$(basename "$file" | sed 's/^[0-9]*_//' | sed 's/.sql$//')
        echo "  - $version: $name"
    done

    echo ""

    if check_migrations_table; then
        print_info "Checking for pending migrations..."

        local applied_versions=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -tAc \
            "SELECT version FROM schema_migrations ORDER BY version;" 2>/dev/null || echo "")

        local pending_count=0

        for file in "${migration_files[@]}"; do
            local version=$(basename "$file" | cut -d'_' -f1)
            if echo "$applied_versions" | grep -q "^$version$"; then
                echo "  ✓ $version (applied)"
            else
                echo "  ✗ $version (pending)"
                ((pending_count++))
            fi
        done

        echo ""

        if [ $pending_count -eq 0 ]; then
            print_success "All migrations are up to date"
        else
            print_warning "$pending_count pending migration(s) to apply"
        fi
    else
        print_warning "schema_migrations table does not exist - all migrations are pending"
    fi
}

apply_all_migrations() {
    print_info "Starting migration process..."

    local migration_files=("$SCRIPT_DIR"/[0-9][0-9][0-9]_*.sql)

    if [ ! -f "${migration_files[0]}" ]; then
        print_error "No migration files found in $SCRIPT_DIR"
        exit 1
    fi

    local current_version=$(get_current_version)
    print_info "Current schema version: $current_version"

    echo ""

    local applied_count=0
    local skipped_count=0

    for migration_file in "${migration_files[@]}"; do
        local version=$(basename "$migration_file" | cut -d'_' -f1)

        # Check if migration is already applied
        if check_migrations_table; then
            local is_applied=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DATABASE" -tAc \
                "SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version = '$version');" 2>/dev/null || echo "f")

            if [ "$is_applied" = "t" ]; then
                print_info "Skipping migration $version (already applied)"
                ((skipped_count++))
                continue
            fi
        fi

        # Apply migration
        if apply_migration "$migration_file"; then
            ((applied_count++))
        else
            print_error "Migration process stopped due to error"
            exit 1
        fi

        echo ""
    done

    echo ""
    print_success "Migration process completed"
    print_info "Applied: $applied_count, Skipped: $skipped_count"

    local new_version=$(get_current_version)
    print_info "New schema version: $new_version"
}

# ============================================================================
# Parse Arguments
# ============================================================================

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--host)
            PG_HOST="$2"
            shift 2
            ;;
        -p|--port)
            PG_PORT="$2"
            shift 2
            ;;
        -d|--database)
            PG_DATABASE="$2"
            shift 2
            ;;
        -u|--user)
            PG_USER="$2"
            shift 2
            ;;
        -v|--verify-only)
            VERIFY_ONLY=true
            shift
            ;;
        --help)
            show_help
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            ;;
    esac
done

# ============================================================================
# Main
# ============================================================================

echo "============================================================================"
echo "Honua Build Orchestrator - Database Migration Tool"
echo "============================================================================"
echo ""
echo "Database: $PG_DATABASE"
echo "Host: $PG_HOST:$PG_PORT"
echo "User: $PG_USER"
echo ""

# Check prerequisites
check_psql

# Test connection
if ! test_connection; then
    exit 1
fi

echo ""

# Verify or apply migrations
if [ "$VERIFY_ONLY" = true ]; then
    verify_migrations
    echo ""
    list_applied_migrations
else
    # Show current state
    list_applied_migrations
    echo ""

    # Confirm before proceeding
    read -p "Apply pending migrations? (y/N): " -n 1 -r
    echo ""

    if [[ $REPLY =~ ^[Yy]$ ]]; then
        apply_all_migrations
    else
        print_info "Migration cancelled by user"
        exit 0
    fi
fi

echo ""
echo "============================================================================"
