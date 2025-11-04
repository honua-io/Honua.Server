#!/bin/bash

# Examples of sending alerts using curl
# Useful for scripts, cron jobs, monitoring systems, etc.

ALERT_RECEIVER_URL="http://alert-receiver:8080"
ALERT_TOKEN="your-secure-token"

# Example 1: Simple critical alert
curl -X POST "$ALERT_RECEIVER_URL/api/alerts" \
  -H "Authorization: Bearer $ALERT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "BackupFailed",
    "severity": "critical",
    "description": "Database backup failed after 3 retry attempts",
    "source": "backup-script",
    "service": "database"
  }'

# Example 2: Alert with labels
curl -X POST "$ALERT_RECEIVER_URL/api/alerts" \
  -H "Authorization: Bearer $ALERT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "DiskSpaceWarning",
    "severity": "medium",
    "description": "Disk usage at 85% on /var/lib/docker",
    "source": "disk-monitor",
    "labels": {
      "mount_point": "/var/lib/docker",
      "percent_used": "85",
      "available_gb": "50"
    }
  }'

# Example 3: Alert with context data
curl -X POST "$ALERT_RECEIVER_URL/api/alerts" \
  -H "Authorization: Bearer $ALERT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "DeploymentFailed",
    "severity": "high",
    "description": "Failed to deploy version v1.2.3 to production",
    "source": "ci-cd-pipeline",
    "service": "deployment",
    "environment": "production",
    "labels": {
      "version": "v1.2.3",
      "deployment_id": "deploy-abc123"
    },
    "context": {
      "git_commit": "a1b2c3d",
      "build_number": 456,
      "failed_at_step": "database_migration"
    }
  }'

# Example 4: Resolved alert
curl -X POST "$ALERT_RECEIVER_URL/api/alerts" \
  -H "Authorization: Bearer $ALERT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ServiceRestored",
    "severity": "info",
    "status": "resolved",
    "description": "Database service has been restored",
    "source": "health-monitor",
    "service": "database"
  }'

# Example 5: Batch alerts
curl -X POST "$ALERT_RECEIVER_URL/api/alerts/batch" \
  -H "Authorization: Bearer $ALERT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "alerts": [
      {
        "name": "HighCPUUsage",
        "severity": "medium",
        "description": "CPU usage at 90% on server-1",
        "source": "monitoring",
        "labels": {"server": "server-1"}
      },
      {
        "name": "HighCPUUsage",
        "severity": "medium",
        "description": "CPU usage at 92% on server-2",
        "source": "monitoring",
        "labels": {"server": "server-2"}
      }
    ]
  }'

# Example 6: From health check script
#!/bin/bash
check_service_health() {
  if ! curl -f http://honua-api:8080/health/ready > /dev/null 2>&1; then
    curl -X POST "$ALERT_RECEIVER_URL/api/alerts" \
      -H "Authorization: Bearer $ALERT_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"HealthCheckFailed\",
        \"severity\": \"critical\",
        \"description\": \"Honua API health check failed\",
        \"source\": \"health-check\",
        \"service\": \"honua-api\",
        \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
      }"
    return 1
  fi
  return 0
}

# Example 7: From backup script with error handling
#!/bin/bash
backup_database() {
  if pg_dump -h localhost -U honua honua > /backups/db-$(date +%Y%m%d).sql; then
    # Success - send info alert
    curl -s -X POST "$ALERT_RECEIVER_URL/api/alerts" \
      -H "Authorization: Bearer $ALERT_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"BackupCompleted\",
        \"severity\": \"info\",
        \"description\": \"Database backup completed successfully\",
        \"source\": \"backup-script\",
        \"labels\": {\"size_mb\": \"$(du -m /backups/db-$(date +%Y%m%d).sql | cut -f1)\"}
      }"
  else
    # Failure - send critical alert
    curl -s -X POST "$ALERT_RECEIVER_URL/api/alerts" \
      -H "Authorization: Bearer $ALERT_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"BackupFailed\",
        \"severity\": \"critical\",
        \"description\": \"Database backup failed\",
        \"source\": \"backup-script\"
      }"
    return 1
  fi
}

# Example 8: From cron job monitoring
#!/bin/bash
# Add to crontab: 0 2 * * * /usr/local/bin/daily-tasks.sh

run_daily_tasks() {
  local start_time=$(date +%s)

  if ! /usr/local/bin/process-analytics.sh; then
    curl -s -X POST "$ALERT_RECEIVER_URL/api/alerts" \
      -H "Authorization: Bearer $ALERT_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"DailyTaskFailed\",
        \"severity\": \"high\",
        \"description\": \"Daily analytics processing failed\",
        \"source\": \"cron-job\",
        \"labels\": {\"task\": \"process-analytics\"}
      }"
    return 1
  fi

  local end_time=$(date +%s)
  local duration=$((end_time - start_time))

  # Alert if task took too long
  if [ $duration -gt 3600 ]; then
    curl -s -X POST "$ALERT_RECEIVER_URL/api/alerts" \
      -H "Authorization: Bearer $ALERT_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"DailyTaskSlow\",
        \"severity\": \"medium\",
        \"description\": \"Daily analytics processing took ${duration}s (>1 hour)\",
        \"source\": \"cron-job\",
        \"labels\": {\"duration_seconds\": \"$duration\"}
      }"
  fi
}
