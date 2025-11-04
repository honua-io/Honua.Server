#!/bin/bash
#===============================================================================
# Azure PostgreSQL Backup Verification Test
#===============================================================================
# Purpose: Verify backup integrity and perform monthly restore tests
# Schedule: Daily for integrity check, Monthly for restore test
# Dependencies: Azure CLI, pg_dump, pg_restore
# Version: 1.0
# Last Updated: 2025-10-18
#===============================================================================

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="${LOG_DIR:-/var/log/honua}"
DATE=$(date +%Y%m%d_%H%M%S)
LOG_FILE="${LOG_DIR}/backup-verify-${DATE}.log"

# Azure Resources
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-honua-prod-eastus}"
SERVER_NAME="${SERVER_NAME:-postgres-honua-prod}"
DB_NAME="${DB_NAME:-honua}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-stbkphonua123456}"
BACKUP_CONTAINER="${BACKUP_CONTAINER:-database-backups}"

# Test Configuration
RUN_RESTORE_TEST="${RUN_RESTORE_TEST:-false}"
TEST_SERVER_PREFIX="postgres-test-restore"
TEST_SERVER_RETENTION_HOURS="${TEST_SERVER_RETENTION_HOURS:-24}"

#===============================================================================
# Logging Functions
#===============================================================================

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

log_error() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $*" | tee -a "$LOG_FILE" >&2
}

log_success() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] ✓ $*" | tee -a "$LOG_FILE"
}

#===============================================================================
# Verification Functions
#===============================================================================

check_backup_existence() {
    log "Checking backup existence..."

    # Check Azure automated backups
    local backup_count=$(az postgres flexible-server backup list \
        --resource-group "$RESOURCE_GROUP" \
        --server-name "$SERVER_NAME" \
        --query 'length(@)' -o tsv 2>/dev/null || echo "0")

    if [ "$backup_count" -eq 0 ]; then
        log_error "No automated backups found"
        return 1
    fi

    log_success "Found $backup_count automated backups"

    # Check latest backup
    local latest_backup=$(az postgres flexible-server backup list \
        --resource-group "$RESOURCE_GROUP" \
        --server-name "$SERVER_NAME" \
        --query '[0].{Name:name, Time:backupStartTime, Type:backupType}' \
        -o json 2>/dev/null)

    log "Latest backup: $latest_backup"
    return 0
}

check_backup_age() {
    log "Checking backup age..."

    local latest_backup_time=$(az postgres flexible-server backup list \
        --resource-group "$RESOURCE_GROUP" \
        --server-name "$SERVER_NAME" \
        --query '[0].backupStartTime' -o tsv 2>/dev/null)

    if [ -z "$latest_backup_time" ]; then
        log_error "Could not determine latest backup time"
        return 1
    fi

    local backup_timestamp=$(date -d "$latest_backup_time" +%s)
    local current_timestamp=$(date +%s)
    local age_seconds=$((current_timestamp - backup_timestamp))
    local age_hours=$((age_seconds / 3600))

    log "Latest backup age: $age_hours hours"

    if [ "$age_hours" -gt 25 ]; then
        log_error "Latest backup is too old: $age_hours hours (max: 25)"
        return 1
    fi

    log_success "Backup age is acceptable: $age_hours hours"
    return 0
}

check_geo_replication() {
    log "Checking geo-replication status..."

    local geo_backup_enabled=$(az postgres flexible-server show \
        --name "$SERVER_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query 'backup.geoRedundantBackup' -o tsv 2>/dev/null)

    if [ "$geo_backup_enabled" != "Enabled" ]; then
        log_error "Geo-redundant backup is not enabled"
        return 1
    fi

    log_success "Geo-redundant backup is enabled"
    return 0
}

check_storage_backups() {
    log "Checking long-term storage backups..."

    # Check daily backups
    local daily_count=$(az storage blob list \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --prefix "daily/" \
        --query 'length(@)' -o tsv 2>/dev/null || echo "0")

    log "Daily backups: $daily_count"

    # Check weekly backups
    local weekly_count=$(az storage blob list \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --prefix "weekly/" \
        --query 'length(@)' -o tsv 2>/dev/null || echo "0")

    log "Weekly backups: $weekly_count"

    # Check monthly backups
    local monthly_count=$(az storage blob list \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --prefix "monthly/" \
        --query 'length(@)' -o tsv 2>/dev/null || echo "0")

    log "Monthly backups: $monthly_count"

    if [ "$daily_count" -eq 0 ] && [ "$weekly_count" -eq 0 ]; then
        log_error "No long-term backups found in storage"
        return 1
    fi

    log_success "Long-term backups found: daily=$daily_count, weekly=$weekly_count, monthly=$monthly_count"
    return 0
}

verify_latest_blob_backup() {
    log "Verifying latest blob backup..."

    # Get latest daily backup
    local latest_blob=$(az storage blob list \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --prefix "daily/" \
        --query 'sort_by(@, &properties.lastModified)[-1].name' -o tsv 2>/dev/null)

    if [ -z "$latest_blob" ]; then
        log_error "No daily backups found"
        return 1
    fi

    log "Latest blob backup: $latest_blob"

    # Check blob size
    local blob_size=$(az storage blob show \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --name "$latest_blob" \
        --query 'properties.contentLength' -o tsv)

    local blob_size_mb=$((blob_size / 1024 / 1024))

    log "Blob size: ${blob_size_mb}MB"

    if [ "$blob_size_mb" -lt 10 ]; then
        log_error "Blob size is suspiciously small: ${blob_size_mb}MB"
        return 1
    fi

    log_success "Latest blob backup verified"
    return 0
}

#===============================================================================
# Restore Test Functions
#===============================================================================

perform_restore_test() {
    log "=========================================="
    log "Starting Monthly Restore Test"
    log "=========================================="

    local test_server="${TEST_SERVER_PREFIX}-$(date +%Y%m)"

    # 1. Create restore point (1 hour ago)
    local restore_time=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)
    log "Creating test restore to point: $restore_time"

    # 2. Create test server from restore point
    log "Creating test server: $test_server"

    az postgres flexible-server restore \
        --resource-group "$RESOURCE_GROUP" \
        --name "$test_server" \
        --source-server "$SERVER_NAME" \
        --restore-time "$restore_time" \
        --location eastus \
        2>&1 | tee -a "$LOG_FILE"

    local restore_status=${PIPESTATUS[0]}

    if [ $restore_status -ne 0 ]; then
        log_error "Restore failed with status $restore_status"
        return 1
    fi

    # 3. Wait for restore completion
    log "Waiting for restore to complete (timeout: 30 minutes)..."

    az postgres flexible-server wait \
        --name "$test_server" \
        --resource-group "$RESOURCE_GROUP" \
        --exists \
        --timeout 1800 \
        2>&1 | tee -a "$LOG_FILE"

    # 4. Verify restored server
    log "Verifying restored server..."

    local test_endpoint=$(az postgres flexible-server show \
        --name "$test_server" \
        --resource-group "$RESOURCE_GROUP" \
        --query 'fullyQualifiedDomainName' -o tsv)

    log "Test server endpoint: $test_endpoint"

    # 5. Run verification queries
    local db_password=$(az keyvault secret show \
        --vault-name "kv-honua-$(echo $RESOURCE_GROUP | grep -oP '(?<=-)[^-]+(?=-)')" \
        --name "PostgreSQL-AdminPassword" \
        --query 'value' -o tsv 2>/dev/null || echo "")

    if [ -z "$db_password" ]; then
        log_error "Could not retrieve database password"
        cleanup_test_server "$test_server"
        return 1
    fi

    # Add firewall rule for our IP
    local my_ip=$(curl -s ifconfig.me)
    az postgres flexible-server firewall-rule create \
        --name "$test_server" \
        --resource-group "$RESOURCE_GROUP" \
        --rule-name VerifyTest \
        --start-ip-address "$my_ip" \
        --end-ip-address "$my_ip" \
        2>/dev/null || true

    # Run verification queries
    log "Running verification queries..."

    PGPASSWORD="$db_password" psql \
        --host="$test_endpoint" \
        --port=5432 \
        --username=honuaadmin \
        --dbname="$DB_NAME" \
        --no-password \
        2>&1 <<EOF | tee -a "$LOG_FILE"
-- Database info
SELECT
    'Database Size' as metric,
    pg_size_pretty(pg_database_size('$DB_NAME')) as value;

-- Table count
SELECT
    'Table Count' as metric,
    COUNT(*)::text as value
FROM information_schema.tables
WHERE table_schema = 'public';

-- Row counts
SELECT
    'Total Rows' as metric,
    SUM(n_live_tup)::text as value
FROM pg_stat_user_tables;

-- Extensions
SELECT
    'PostGIS Version' as metric,
    PostGIS_Version() as value;

-- Latest data timestamp
SELECT
    'Latest Data' as metric,
    MAX(updated_at)::text as value
FROM audit_log;
EOF

    local verify_status=$?

    # 6. Generate test report
    generate_restore_test_report "$test_server" "$restore_time" "$verify_status"

    # 7. Schedule cleanup
    schedule_test_server_cleanup "$test_server"

    if [ $verify_status -eq 0 ]; then
        log_success "Restore test completed successfully"
        return 0
    else
        log_error "Restore test verification failed"
        return 1
    fi
}

generate_restore_test_report() {
    local test_server="$1"
    local restore_time="$2"
    local status="$3"

    local report_file="${LOG_DIR}/restore-test-$(date +%Y%m).txt"

    cat > "$report_file" <<EOF
Monthly Restore Test Report
===========================
Date: $(date)
Test Server: $test_server
Source Server: $SERVER_NAME
Restore Point: $restore_time

Test Results:
- Server Creation: SUCCESS
- Data Verification: $([ $status -eq 0 ] && echo "SUCCESS" || echo "FAILED")
- Status Code: $status

Database Details:
$(PGPASSWORD="$db_password" psql --host="$test_endpoint" --port=5432 --username=honuaadmin --dbname="$DB_NAME" --no-password -t -c "SELECT pg_size_pretty(pg_database_size('$DB_NAME'));" 2>/dev/null || echo "N/A")

Cleanup Schedule:
- Server will be deleted after $TEST_SERVER_RETENTION_HOURS hours

Overall Status: $([ $status -eq 0 ] && echo "✓ PASS" || echo "✗ FAIL")
EOF

    log "Test report generated: $report_file"

    # Upload report to storage
    az storage blob upload \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --name "restore-tests/report-$(date +%Y%m).txt" \
        --file "$report_file" \
        --tier Hot \
        --overwrite \
        2>/dev/null || true
}

schedule_test_server_cleanup() {
    local test_server="$1"

    log "Scheduling cleanup of test server after $TEST_SERVER_RETENTION_HOURS hours"

    # Create a cleanup script
    cat > "/tmp/cleanup-${test_server}.sh" <<EOF
#!/bin/bash
sleep $((TEST_SERVER_RETENTION_HOURS * 3600))
az postgres flexible-server delete \\
    --name "$test_server" \\
    --resource-group "$RESOURCE_GROUP" \\
    --yes \\
    2>/dev/null || true
echo "Test server deleted: $test_server"
EOF

    chmod +x "/tmp/cleanup-${test_server}.sh"
    nohup "/tmp/cleanup-${test_server}.sh" &>/dev/null &

    log "Cleanup scheduled for test server: $test_server"
}

cleanup_test_server() {
    local test_server="$1"

    log "Cleaning up test server: $test_server"

    az postgres flexible-server delete \
        --name "$test_server" \
        --resource-group "$RESOURCE_GROUP" \
        --yes \
        2>&1 | tee -a "$LOG_FILE"

    log "Test server deleted: $test_server"
}

#===============================================================================
# Main Function
#===============================================================================

main() {
    log "=========================================="
    log "Honua Backup Verification Starting"
    log "=========================================="

    local failed_checks=0

    # 1. Check backup existence
    if ! check_backup_existence; then
        ((failed_checks++))
    fi

    # 2. Check backup age
    if ! check_backup_age; then
        ((failed_checks++))
    fi

    # 3. Check geo-replication
    if ! check_geo_replication; then
        ((failed_checks++))
    fi

    # 4. Check storage backups
    if ! check_storage_backups; then
        ((failed_checks++))
    fi

    # 5. Verify latest blob backup
    if ! verify_latest_blob_backup; then
        ((failed_checks++))
    fi

    # 6. Perform restore test (if monthly schedule)
    if [ "$RUN_RESTORE_TEST" == "true" ]; then
        if ! perform_restore_test; then
            ((failed_checks++))
        fi
    else
        log "Skipping restore test (RUN_RESTORE_TEST=false)"
    fi

    # Summary
    log "=========================================="
    if [ $failed_checks -eq 0 ]; then
        log_success "All verification checks passed"
        exit 0
    else
        log_error "$failed_checks verification check(s) failed"
        exit 1
    fi
}

# Run main function
main "$@"
