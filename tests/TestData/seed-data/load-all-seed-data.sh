#!/bin/bash

################################################################################
# Honua Seed Data Loader
#
# This script loads all seed data GeoJSON files into Honua.
# It performs health checks, service validation, and provides detailed progress.
#
# Usage: ./load-all-seed-data.sh [options]
# Options:
#   --help                Show this help message
#   --dry-run            Show what would be loaded without actually loading
#   --verbose            Show detailed output
#   --retry-count N      Number of retries for failed imports (default: 3)
#   --timeout N          Timeout in seconds for HTTP requests (default: 30)
################################################################################

set -o pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../../" && pwd)"
HONUA_CLI_PROJECT="$PROJECT_ROOT/src/Honua.Cli"
HONUA_SERVER_URL="${HONUA_SERVER_URL:-http://localhost:5000}"
SERVICE_NAME="seed-data"
RETRY_COUNT=3
HTTP_TIMEOUT=30
DRY_RUN=false
VERBOSE=false

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Statistics
TOTAL_FILES=0
SUCCESSFUL_IMPORTS=0
FAILED_IMPORTS=0
SKIPPED_FILES=0
TOTAL_FEATURES=0
TOTAL_SIZE=0

# Define seed data files: filename -> layer name
declare -A SEED_DATA=(
    [cities.geojson]="cities"
    [poi.geojson]="poi"
    [roads.geojson]="roads"
    [transit_routes.geojson]="transit_routes"
    [parcels.geojson]="parcels"
    [parks.geojson]="parks"
    [water_bodies.geojson]="water_bodies"
    [administrative_boundaries.geojson]="administrative_boundaries"
    [weather_stations.geojson]="weather_stations"
)

################################################################################
# Utility Functions
################################################################################

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
    echo -e "${RED}[ERROR]${NC} $*" >&2
}

log_verbose() {
    if [[ "$VERBOSE" == true ]]; then
        echo -e "${CYAN}[VERBOSE]${NC} $*"
    fi
}

print_header() {
    echo ""
    echo -e "${CYAN}================================================================================${NC}"
    echo -e "${CYAN}$*${NC}"
    echo -e "${CYAN}================================================================================${NC}"
}

print_progress() {
    local current=$1
    local total=$2
    local filename=$3
    local status=$4

    local percent=$((current * 100 / total))
    printf "[%3d%%] [%2d/%2d] %-40s %s\n" "$percent" "$current" "$total" "$filename" "$status"
}

show_help() {
    grep '^#' "$0" | grep -E '^\s*#' | head -20
    exit 0
}

################################################################################
# Health Checks
################################################################################

check_honua_cli() {
    log_info "Checking Honua CLI..."

    if [[ ! -d "$HONUA_CLI_PROJECT" ]]; then
        log_error "Honua CLI project not found at: $HONUA_CLI_PROJECT"
        return 1
    fi

    log_verbose "Honua CLI project found at: $HONUA_CLI_PROJECT"
    log_success "Honua CLI is available"
    return 0
}

check_server_health() {
    log_info "Checking Honua server health at: $HONUA_SERVER_URL"

    local retries=5
    local wait_time=2

    for ((i = 1; i <= retries; i++)); do
        if curl -s -f -m "$HTTP_TIMEOUT" "$HONUA_SERVER_URL/health/live" >/dev/null 2>&1; then
            log_success "Honua server is running and healthy"
            return 0
        fi

        if [[ $i -lt $retries ]]; then
            log_warning "Server not responding (attempt $i/$retries), retrying in ${wait_time}s..."
            sleep "$wait_time"
        fi
    done

    log_error "Honua server is not responding at $HONUA_SERVER_URL"
    log_error "Please ensure the server is running: dotnet run --project src/Honua.Server.Host"
    return 1
}

check_seed_data_files() {
    log_info "Checking seed data files..."

    local missing_files=0

    for filename in "${!SEED_DATA[@]}"; do
        local filepath="$SCRIPT_DIR/$filename"

        if [[ ! -f "$filepath" ]]; then
            log_warning "Missing seed data file: $filename"
            ((missing_files++))
        else
            local size=$(stat -f%z "$filepath" 2>/dev/null || stat -c%s "$filepath" 2>/dev/null || echo "0")
            local features=$(jq '.features | length' "$filepath" 2>/dev/null || echo "?")

            log_verbose "Found: $filename ($features features, $(numfmt --to=iec-i --suffix=B $size 2>/dev/null || echo "$size bytes"))"
            TOTAL_SIZE=$((TOTAL_SIZE + size))
            TOTAL_FEATURES=$((TOTAL_FEATURES + features))
        fi
    done

    TOTAL_FILES=${#SEED_DATA[@]}
    SKIPPED_FILES=$missing_files

    if [[ $missing_files -gt 0 ]]; then
        log_warning "Found $missing_files missing seed data file(s)"
    fi

    log_success "Found $((TOTAL_FILES - missing_files))/$TOTAL_FILES seed data files"
    return 0
}

################################################################################
# Service Management
################################################################################

create_service_if_needed() {
    log_info "Checking if service '$SERVICE_NAME' exists..."

    local response=$(curl -s -m "$HTTP_TIMEOUT" \
        -X GET "$HONUA_SERVER_URL/api/v1/services/$SERVICE_NAME" \
        -H "Content-Type: application/json" 2>/dev/null)

    if echo "$response" | jq . >/dev/null 2>&1; then
        log_success "Service '$SERVICE_NAME' already exists"
        return 0
    fi

    log_info "Creating service '$SERVICE_NAME'..."

    local service_config=$(cat <<EOF
{
    "name": "$SERVICE_NAME",
    "description": "Seed data service for testing",
    "dataStoreType": "InMemory",
    "cachingEnabled": true,
    "cacheExpirationMinutes": 60
}
EOF
)

    if [[ "$DRY_RUN" == true ]]; then
        log_verbose "DRY RUN - Would create service with: $service_config"
        return 0
    fi

    local response=$(curl -s -m "$HTTP_TIMEOUT" \
        -X POST "$HONUA_SERVER_URL/api/v1/services" \
        -H "Content-Type: application/json" \
        -d "$service_config" 2>/dev/null)

    if echo "$response" | jq -e '.name == "'$SERVICE_NAME'"' >/dev/null 2>&1; then
        log_success "Service '$SERVICE_NAME' created successfully"
        return 0
    else
        log_warning "Could not verify service creation (may already exist)"
        return 0
    fi
}

################################################################################
# Data Import
################################################################################

get_feature_count() {
    local filepath=$1

    if [[ ! -f "$filepath" ]]; then
        echo "0"
        return
    fi

    jq '.features | length' "$filepath" 2>/dev/null || echo "0"
}

import_geojson_file() {
    local filename=$1
    local layer_name=$2
    local attempt=1

    local filepath="$SCRIPT_DIR/$filename"

    if [[ ! -f "$filepath" ]]; then
        log_warning "Skipping: $filename (file not found)"
        return 2
    fi

    local feature_count=$(get_feature_count "$filepath")

    while [[ $attempt -le $RETRY_COUNT ]]; do
        if [[ "$DRY_RUN" == true ]]; then
            log_verbose "DRY RUN - Would import: $filename → layer: $layer_name ($feature_count features)"
            return 0
        fi

        log_verbose "Importing $filename (attempt $attempt/$RETRY_COUNT)..."

        local output
        local exit_code

        output=$(cd "$PROJECT_ROOT" && \
            dotnet run --project "$HONUA_CLI_PROJECT" -- data ingest \
            --service-id "$SERVICE_NAME" \
            --layer-id "$layer_name" \
            --host "$HONUA_SERVER_URL" \
            "$filepath" 2>&1)
        exit_code=$?

        if [[ $exit_code -eq 0 ]]; then
            log_verbose "Import output: $output"
            return 0
        fi

        if [[ $attempt -lt $RETRY_COUNT ]]; then
            log_warning "Import failed (attempt $attempt/$RETRY_COUNT): $filename"
            sleep 2
        fi

        ((attempt++))
    done

    log_error "Failed to import $filename after $RETRY_COUNT attempts"
    log_verbose "Last error output: $output"
    return 1
}

################################################################################
# Main Import Loop
################################################################################

import_all_seed_data() {
    print_header "Loading Seed Data"

    local current=0

    for filename in "${!SEED_DATA[@]}"; do
        ((current++))
        local layer_name=${SEED_DATA[$filename]}
        local filepath="$SCRIPT_DIR/$filename"

        if [[ ! -f "$filepath" ]]; then
            print_progress "$current" "$TOTAL_FILES" "$filename" "SKIPPED (not found)"
            continue
        fi

        print_progress "$current" "$TOTAL_FILES" "$filename" "IMPORTING..."

        if import_geojson_file "$filename" "$layer_name"; then
            print_progress "$current" "$TOTAL_FILES" "$filename" "OK"
            ((SUCCESSFUL_IMPORTS++))
        else
            print_progress "$current" "$TOTAL_FILES" "$filename" "FAILED"
            ((FAILED_IMPORTS++))
        fi
    done
}

################################################################################
# Summary and Reporting
################################################################################

print_summary() {
    print_header "Import Summary"

    local loaded_files=$((TOTAL_FILES - SKIPPED_FILES))

    echo ""
    echo "Files Statistics:"
    echo "  Total files to load:     $TOTAL_FILES"
    echo "  Successfully imported:   ${GREEN}$SUCCESSFUL_IMPORTS${NC}"
    echo "  Failed imports:          $([ $FAILED_IMPORTS -eq 0 ] && echo -e "${GREEN}0${NC}" || echo -e "${RED}$FAILED_IMPORTS${NC}")"
    echo "  Skipped (not found):     $SKIPPED_FILES"
    echo ""

    if [[ $TOTAL_FEATURES -gt 0 ]]; then
        echo "Data Statistics:"
        echo "  Total features:          $TOTAL_FEATURES"
        echo "  Total size:              $(numfmt --to=iec-i --suffix=B $TOTAL_SIZE 2>/dev/null || echo "$TOTAL_SIZE bytes")"
        echo ""
    fi

    echo "Service Information:"
    echo "  Service name:            $SERVICE_NAME"
    echo "  Server URL:              $HONUA_SERVER_URL"
    echo ""

    if [[ $DRY_RUN == true ]]; then
        echo -e "${YELLOW}This was a DRY RUN - no data was actually imported${NC}"
        echo ""
    fi

    if [[ $FAILED_IMPORTS -eq 0 ]]; then
        log_success "All seed data loaded successfully!"
        return 0
    else
        log_error "Some imports failed. Please review the errors above."
        return 1
    fi
}

################################################################################
# Main Execution
################################################################################

main() {
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --help)
                show_help
                ;;
            --dry-run)
                DRY_RUN=true
                log_info "DRY RUN mode enabled"
                shift
                ;;
            --verbose)
                VERBOSE=true
                shift
                ;;
            --retry-count)
                RETRY_COUNT=$2
                shift 2
                ;;
            --timeout)
                HTTP_TIMEOUT=$2
                shift 2
                ;;
            *)
                log_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done

    print_header "Honua Seed Data Loader"
    echo "Started at: $(date '+%Y-%m-%d %H:%M:%S')"
    echo ""

    # Run health checks
    if ! check_honua_cli; then
        log_error "Honua CLI check failed"
        exit 1
    fi

    echo ""

    if ! check_server_health; then
        log_error "Server health check failed"
        exit 1
    fi

    echo ""

    if ! check_seed_data_files; then
        log_error "Seed data file check failed"
        exit 1
    fi

    echo ""

    if ! create_service_if_needed; then
        log_error "Service creation failed"
        exit 1
    fi

    echo ""

    # Import all seed data
    import_all_seed_data

    echo ""

    # Print summary
    print_summary
    local summary_exit_code=$?

    echo ""
    echo "Completed at: $(date '+%Y-%m-%d %H:%M:%S')"

    exit $summary_exit_code
}

# Run main function
main "$@"
