#!/bin/bash
#===============================================================================
# Azure PostgreSQL Backup Automation Script
#===============================================================================
# Purpose: Automate database backups with verification and long-term storage
# Schedule: Daily at 2:00 AM UTC
# Dependencies: Azure CLI, pg_dump, jq
# Version: 1.0
# Last Updated: 2025-10-18
#===============================================================================

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="${LOG_DIR:-/var/log/honua}"
BACKUP_DIR="${BACKUP_DIR:-/tmp/honua-backups}"
DATE=$(date +%Y%m%d_%H%M%S)
LOG_FILE="${LOG_DIR}/backup-${DATE}.log"

# Azure Resources
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-honua-prod-eastus}"
SERVER_NAME="${SERVER_NAME:-postgres-honua-prod}"
DB_NAME="${DB_NAME:-honua}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-stbkphonua123456}"
BACKUP_CONTAINER="${BACKUP_CONTAINER:-database-backups}"

# Backup Configuration
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-90}"
COMPRESSION_LEVEL="${COMPRESSION_LEVEL:-9}"
PARALLEL_JOBS="${PARALLEL_JOBS:-4}"
MAX_BACKUP_SIZE_GB="${MAX_BACKUP_SIZE_GB:-100}"

# Alert Configuration
ALERT_EMAIL="${ALERT_EMAIL:-ops@honua.io}"
SLACK_WEBHOOK="${SLACK_WEBHOOK:-}"
PAGERDUTY_KEY="${PAGERDUTY_KEY:-}"

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
# Alert Functions
#===============================================================================

send_alert() {
    local severity="$1"
    local message="$2"

    log "Sending alert: [$severity] $message"

    # Email alert
    if [ -n "$ALERT_EMAIL" ]; then
        echo "$message" | mail -s "Honua Backup Alert [$severity]" "$ALERT_EMAIL"
    fi

    # Slack alert
    if [ -n "$SLACK_WEBHOOK" ]; then
        curl -X POST "$SLACK_WEBHOOK" \
            -H 'Content-Type: application/json' \
            -d "{\"text\":\"🚨 Backup Alert [$severity]: $message\"}" \
            2>/dev/null || true
    fi

    # PagerDuty alert (for critical issues)
    if [ -n "$PAGERDUTY_KEY" ] && [ "$severity" == "CRITICAL" ]; then
        curl -X POST "https://events.pagerduty.com/v2/enqueue" \
            -H 'Content-Type: application/json' \
            -d "{
                \"routing_key\":\"$PAGERDUTY_KEY\",
                \"event_action\":\"trigger\",
                \"payload\":{
                    \"summary\":\"Honua Backup Failure\",
                    \"severity\":\"critical\",
                    \"source\":\"backup-automation\",
                    \"custom_details\":{\"message\":\"$message\"}
                }
            }" \
            2>/dev/null || true
    fi
}

send_metric() {
    local metric_name="$1"
    local metric_value="$2"

    # Send to Azure Monitor custom metrics
    # Requires: az monitor metrics
    log "Metric: $metric_name = $metric_value"

    # Optional: Send to Prometheus Pushgateway
    if [ -n "${PROMETHEUS_PUSHGATEWAY:-}" ]; then
        echo "honua_backup_$metric_name $metric_value" | \
            curl --data-binary @- \
            "$PROMETHEUS_PUSHGATEWAY/metrics/job/backup-automation" \
            2>/dev/null || true
    fi
}

#===============================================================================
# Pre-flight Checks
#===============================================================================

preflight_checks() {
    log "Running pre-flight checks..."

    # Check required commands
    local required_commands=("az" "pg_dump" "pg_restore" "jq" "gzip")
    for cmd in "${required_commands[@]}"; do
        if ! command -v "$cmd" &> /dev/null; then
            log_error "Required command not found: $cmd"
            return 1
        fi
    done

    # Check Azure CLI authentication
    if ! az account show &> /dev/null; then
        log_error "Azure CLI not authenticated. Run: az login"
        return 1
    fi

    # Check Azure resources exist
    if ! az postgres flexible-server show \
        --name "$SERVER_NAME" \
        --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        log_error "PostgreSQL server not found: $SERVER_NAME"
        return 1
    fi

    # Check storage account exists
    if ! az storage account show \
        --name "$STORAGE_ACCOUNT" \
        --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        log_error "Storage account not found: $STORAGE_ACCOUNT"
        return 1
    fi

    # Create directories
    mkdir -p "$LOG_DIR" "$BACKUP_DIR"

    # Check disk space (need at least 50GB free)
    local free_space=$(df -BG "$BACKUP_DIR" | awk 'NR==2 {print $4}' | sed 's/G//')
    if [ "$free_space" -lt 50 ]; then
        log_error "Insufficient disk space: ${free_space}GB free (need 50GB)"
        return 1
    fi

    log_success "Pre-flight checks passed"
    return 0
}

#===============================================================================
# Backup Functions
#===============================================================================

create_backup() {
    log "Starting database backup..."

    local backup_name="postgres-${SERVER_NAME}-${DATE}"
    local backup_file="${BACKUP_DIR}/${backup_name}.dump"
    local compressed_file="${backup_file}.gz"

    # Get database connection info
    local db_host=$(az postgres flexible-server show \
        --name "$SERVER_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query 'fullyQualifiedDomainName' -o tsv)

    # Get admin credentials from Key Vault
    local db_user=$(az keyvault secret show \
        --vault-name "kv-honua-$(echo $RESOURCE_GROUP | grep -oP '(?<=-)[^-]+(?=-)')" \
        --name "PostgreSQL-AdminUser" \
        --query 'value' -o tsv 2>/dev/null || echo "honuaadmin")

    local db_password=$(az keyvault secret show \
        --vault-name "kv-honua-$(echo $RESOURCE_GROUP | grep -oP '(?<=-)[^-]+(?=-)')" \
        --name "PostgreSQL-AdminPassword" \
        --query 'value' -o tsv)

    # Record start time
    local start_time=$(date +%s)

    # Perform backup with parallel jobs
    log "Backing up database: $DB_NAME"
    PGPASSWORD="$db_password" pg_dump \
        --host="$db_host" \
        --port=5432 \
        --username="$db_user" \
        --dbname="$DB_NAME" \
        --format=custom \
        --compress=0 \
        --jobs="$PARALLEL_JOBS" \
        --verbose \
        --file="$backup_file" \
        2>&1 | tee -a "$LOG_FILE"

    local dump_status=${PIPESTATUS[0]}

    # Record end time
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))

    if [ $dump_status -ne 0 ]; then
        log_error "pg_dump failed with status $dump_status"
        send_alert "CRITICAL" "Database backup failed for $SERVER_NAME"
        return 1
    fi

    # Verify backup file exists and has reasonable size
    if [ ! -f "$backup_file" ]; then
        log_error "Backup file not created: $backup_file"
        send_alert "CRITICAL" "Backup file not found"
        return 1
    fi

    local backup_size_bytes=$(stat -c%s "$backup_file")
    local backup_size_mb=$((backup_size_bytes / 1024 / 1024))
    local backup_size_gb=$((backup_size_mb / 1024))

    log "Backup size: ${backup_size_mb}MB (${backup_size_gb}GB)"

    # Check if backup size is reasonable (not too small, not too large)
    if [ "$backup_size_mb" -lt 10 ]; then
        log_error "Backup size suspiciously small: ${backup_size_mb}MB"
        send_alert "WARNING" "Backup size is only ${backup_size_mb}MB"
    fi

    if [ "$backup_size_gb" -gt "$MAX_BACKUP_SIZE_GB" ]; then
        log_error "Backup size exceeds limit: ${backup_size_gb}GB > ${MAX_BACKUP_SIZE_GB}GB"
        send_alert "WARNING" "Backup size is ${backup_size_gb}GB (limit: ${MAX_BACKUP_SIZE_GB}GB)"
    fi

    # Compress backup
    log "Compressing backup..."
    gzip -"$COMPRESSION_LEVEL" "$backup_file"

    local compressed_size_bytes=$(stat -c%s "$compressed_file")
    local compressed_size_mb=$((compressed_size_bytes / 1024 / 1024))
    local compression_ratio=$((100 - (compressed_size_bytes * 100 / backup_size_bytes)))

    log "Compressed size: ${compressed_size_mb}MB (${compression_ratio}% compression)"

    # Send metrics
    send_metric "backup_duration_seconds" "$duration"
    send_metric "backup_size_bytes" "$backup_size_bytes"
    send_metric "backup_compressed_size_bytes" "$compressed_size_bytes"

    log_success "Backup created: $compressed_file"
    echo "$compressed_file"
}

verify_backup() {
    local backup_file="$1"

    log "Verifying backup integrity..."

    # Check file exists
    if [ ! -f "$backup_file" ]; then
        log_error "Backup file not found: $backup_file"
        return 1
    fi

    # Test gunzip
    if ! gzip -t "$backup_file" 2>/dev/null; then
        log_error "Backup file is corrupted (gzip test failed)"
        return 1
    fi

    # Test pg_restore --list
    if ! pg_restore --list "$backup_file" > /dev/null 2>&1; then
        log_error "Backup file is corrupted (pg_restore test failed)"
        return 1
    fi

    # Count tables in backup
    local table_count=$(pg_restore --list "$backup_file" 2>/dev/null | grep -c "TABLE DATA" || true)

    log "Backup contains $table_count tables"

    if [ "$table_count" -lt 5 ]; then
        log_error "Suspiciously few tables in backup: $table_count"
        return 1
    fi

    log_success "Backup verification passed"
    send_metric "backup_verification_success" "1"
    return 0
}

upload_to_storage() {
    local backup_file="$1"
    local backup_name=$(basename "$backup_file")

    log "Uploading backup to Azure Blob Storage..."

    # Determine blob path based on day of week/month
    local day_of_week=$(date +%u)  # 1-7 (Monday-Sunday)
    local day_of_month=$(date +%d)

    local blob_path=""
    if [ "$day_of_month" == "01" ]; then
        # First of month = monthly backup
        blob_path="monthly/$backup_name"
    elif [ "$day_of_week" == "7" ]; then
        # Sunday = weekly backup
        blob_path="weekly/$backup_name"
    else
        # Regular daily backup
        blob_path="daily/$backup_name"
    fi

    # Upload to Azure Blob Storage
    az storage blob upload \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --name "$blob_path" \
        --file "$backup_file" \
        --tier Hot \
        --metadata "backup_date=$DATE" "server=$SERVER_NAME" "database=$DB_NAME" \
        --overwrite \
        2>&1 | tee -a "$LOG_FILE"

    local upload_status=${PIPESTATUS[0]}

    if [ $upload_status -ne 0 ]; then
        log_error "Failed to upload backup to Azure Storage"
        send_alert "CRITICAL" "Backup upload failed"
        return 1
    fi

    # Verify upload
    local uploaded_size=$(az storage blob show \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --name "$blob_path" \
        --query 'properties.contentLength' -o tsv)

    local local_size=$(stat -c%s "$backup_file")

    if [ "$uploaded_size" != "$local_size" ]; then
        log_error "Upload size mismatch: local=$local_size, uploaded=$uploaded_size"
        send_alert "CRITICAL" "Backup upload incomplete"
        return 1
    fi

    log_success "Backup uploaded: $blob_path"
    send_metric "backup_upload_success" "1"
    return 0
}

cleanup_old_backups() {
    log "Cleaning up old backups..."

    # Cleanup local backups
    log "Removing local backups older than 7 days..."
    find "$BACKUP_DIR" -name "postgres-*.dump.gz" -mtime +7 -delete

    # Cleanup old daily backups from storage (keep only $BACKUP_RETENTION_DAYS)
    log "Removing daily backups older than $BACKUP_RETENTION_DAYS days from storage..."

    local cutoff_date=$(date -d "$BACKUP_RETENTION_DAYS days ago" +%Y%m%d)

    az storage blob list \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --prefix "daily/" \
        --query "[?properties.lastModified<'$cutoff_date'].name" \
        -o tsv | while read -r blob_name; do
            log "Deleting old backup: $blob_name"
            az storage blob delete \
                --account-name "$STORAGE_ACCOUNT" \
                --container-name "$BACKUP_CONTAINER" \
                --name "$blob_name" \
                2>/dev/null || true
        done

    log_success "Cleanup completed"
}

generate_backup_report() {
    local backup_file="$1"
    local backup_size=$(stat -c%s "$backup_file" 2>/dev/null || echo "0")
    local backup_size_mb=$((backup_size / 1024 / 1024))

    local report_file="${LOG_DIR}/backup-report-${DATE}.txt"

    cat > "$report_file" <<EOF
Honua Database Backup Report
=============================
Date: $(date)
Server: $SERVER_NAME
Database: $DB_NAME
Resource Group: $RESOURCE_GROUP

Backup Details:
- Backup File: $(basename "$backup_file")
- Size: ${backup_size_mb}MB
- Compression: Level $COMPRESSION_LEVEL
- Parallel Jobs: $PARALLEL_JOBS

Verification:
- Integrity Check: PASSED
- Upload to Azure: SUCCESS
- Storage Account: $STORAGE_ACCOUNT
- Container: $BACKUP_CONTAINER

Retention:
- Daily Backups: $BACKUP_RETENTION_DAYS days
- Weekly Backups: 1 year
- Monthly Backups: 7 years

Next Backup: $(date -d "tomorrow 2:00 AM" +"%Y-%m-%d %H:%M")

Status: ✓ SUCCESS
EOF

    log "Backup report generated: $report_file"

    # Upload report to storage
    az storage blob upload \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$BACKUP_CONTAINER" \
        --name "reports/backup-report-${DATE}.txt" \
        --file "$report_file" \
        --tier Hot \
        2>/dev/null || true
}

#===============================================================================
# Main Function
#===============================================================================

main() {
    log "=========================================="
    log "Honua Database Backup Starting"
    log "=========================================="

    # Pre-flight checks
    if ! preflight_checks; then
        log_error "Pre-flight checks failed"
        send_alert "CRITICAL" "Backup pre-flight checks failed"
        exit 1
    fi

    # Create backup
    local backup_file
    if ! backup_file=$(create_backup); then
        log_error "Backup creation failed"
        send_alert "CRITICAL" "Database backup failed"
        exit 1
    fi

    # Verify backup
    if ! verify_backup "$backup_file"; then
        log_error "Backup verification failed"
        send_alert "CRITICAL" "Backup verification failed"
        exit 1
    fi

    # Upload to Azure Storage
    if ! upload_to_storage "$backup_file"; then
        log_error "Backup upload failed"
        send_alert "CRITICAL" "Backup upload failed"
        exit 1
    fi

    # Cleanup old backups
    cleanup_old_backups

    # Generate report
    generate_backup_report "$backup_file"

    # Success metrics
    send_metric "backup_success" "1"
    send_metric "backup_last_success_timestamp" "$(date +%s)"

    log "=========================================="
    log "Backup Completed Successfully"
    log "=========================================="

    # Optional: Send success notification
    if [ "${NOTIFY_SUCCESS:-false}" == "true" ]; then
        send_alert "INFO" "Database backup completed successfully"
    fi

    exit 0
}

# Trap errors
trap 'log_error "Backup failed at line $LINENO"; send_alert "CRITICAL" "Backup script failed"; exit 1' ERR

# Run main function
main "$@"
