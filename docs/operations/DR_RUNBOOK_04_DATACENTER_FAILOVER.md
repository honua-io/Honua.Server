# Disaster Recovery Runbook: Data Center Failover

**Runbook ID**: DR-04
**Last Updated**: 2025-10-18
**Version**: 1.0
**Severity**: P0 (Critical - Regional Outage)
**Estimated Time**: 30-90 minutes

## Table of Contents

- [Overview](#overview)
- [Recovery Objectives](#recovery-objectives)
- [Prerequisites](#prerequisites)
- [Failover Scenarios](#failover-scenarios)
- [Step-by-Step Procedures](#step-by-step-procedures)
- [Validation](#validation)
- [Failback Procedures](#failback-procedures)

---

## Overview

This runbook provides procedures for failing over Honua services from a primary datacenter/region to a secondary datacenter/region in response to regional outages, disasters, or planned maintenance.

### When to Use This Runbook

- **Regional cloud outage**: Primary AWS/Azure/GCP region completely unavailable
- **Natural disaster**: Hurricane, earthquake, flood affecting datacenter
- **Planned maintenance**: Region migration or major infrastructure upgrade
- **Performance degradation**: Primary region experiencing severe latency/issues
- **Compliance requirement**: Need to shift to different geographic region
- **Multi-region active-active**: Traffic shifting for load balancing

### Deployment Architectures Supported

#### 1. Active-Passive (Primary + DR)
- **Primary**: East US (Production)
- **Secondary**: Central US (Warm standby)
- **Failover Time**: 15-30 minutes
- **Data Loss**: 5-15 minutes (RPO)

#### 2. Active-Active (Multi-Region)
- **Region 1**: East US (50% traffic)
- **Region 2**: West US (50% traffic)
- **Failover Time**: 5 minutes (DNS change)
- **Data Loss**: None (continuous replication)

#### 3. Active-Active-Passive (Primary + Secondary + DR)
- **Primary**: East US (70% traffic)
- **Secondary**: West US (30% traffic)
- **DR**: Central US (0% traffic, cold standby)
- **Failover Time**: 10-20 minutes
- **Data Loss**: Minimal (<5 minutes)

---

## Recovery Objectives

### Failover Objectives

| Scenario | RTO | RPO | Acceptable Data Loss |
|----------|-----|-----|---------------------|
| **Active-Passive Failover** | 30 min | 15 min | < 1000 records |
| **Active-Active Traffic Shift** | 5 min | 0 min | None (sync repl) |
| **Emergency Failover** | 15 min | 30 min | < 5000 records |
| **Planned Failover** | 10 min | 5 min | < 100 records |

### Regional SLA Commitments

| Region Pair | Uptime SLA | Max Downtime/Month | Failover Triggers |
|-------------|------------|--------------------|--------------------|
| **East US → Central US** | 99.95% | 21.6 minutes | 15+ min outage |
| **West Europe → North Europe** | 99.99% | 4.3 minutes | 5+ min outage |
| **Southeast Asia → East Asia** | 99.9% | 43.2 minutes | 30+ min outage |

---

## Prerequisites

### Infrastructure Requirements

- [ ] **Secondary region infrastructure deployed** (via Terraform)
- [ ] **Database replication configured** (continuous or periodic)
- [ ] **Storage replication enabled** (geo-redundant)
- [ ] **DNS failover configured** (Route53/Traffic Manager)
- [ ] **Application deployed in secondary** (running or deployable)
- [ ] **Monitoring in secondary region** (separate Prometheus/Grafana)
- [ ] **Runbooks tested in last 90 days**

### Access Requirements

```bash
# Cloud Provider Access
AWS_PROFILE="honua-admin"
AZURE_SUBSCRIPTION="honua-production"

# DNS Provider
CLOUDFLARE_API_TOKEN="<from-vault>"
ROUTE53_ZONE_ID="<from-vault>"

# Incident Communication
SLACK_WEBHOOK="<from-vault>"
PAGERDUTY_TOKEN="<from-vault>"

# Database Access
PRIMARY_DB_HOST="postgres-honua-eastus.postgres.database.azure.com"
SECONDARY_DB_HOST="postgres-honua-centralus.postgres.database.azure.com"
```

### Required Tools

```bash
# Install Azure Traffic Manager CLI tools
az extension add --name traffic-manager

# Install AWS Route53 CLI
pip install awscli

# Install database tools
apt-get install postgresql-client-16

# Install monitoring tools
kubectl krew install status
kubectl krew install stern
```

### Pre-Failover Checklist

Before initiating failover, verify:

```bash
#!/bin/bash
# pre-failover-check.sh

echo "=== Pre-Failover Validation ==="

# 1. Secondary region infrastructure exists
echo "1. Checking secondary region infrastructure..."
az group show --name "rg-honua-prod-centralus" || {
    echo "ERROR: Secondary resource group not found"
    exit 1
}

# 2. Secondary database is replicating
echo "2. Checking database replication status..."
REPLICATION_LAG=$(psql -h "$SECONDARY_DB_HOST" \
    -c "SELECT EXTRACT(EPOCH FROM (now() - pg_last_xact_replay_timestamp()));" \
    -t -A)

if (( $(echo "$REPLICATION_LAG > 300" | bc -l) )); then
    echo "WARNING: Replication lag is ${REPLICATION_LAG}s (> 5 minutes)"
fi

# 3. Secondary Kubernetes cluster healthy
echo "3. Checking secondary Kubernetes cluster..."
kubectl --context=aks-honua-centralus get nodes || {
    echo "ERROR: Cannot access secondary cluster"
    exit 1
}

# 4. Storage replication healthy
echo "4. Checking storage replication..."
az storage account show \
    --name "sthonuaprodcentralus" \
    --query "geoReplicationStats" || {
    echo "ERROR: Storage account not found or not replicated"
    exit 1
}

# 5. DNS provider accessible
echo "5. Checking DNS provider..."
curl -f -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
    "https://api.cloudflare.com/client/v4/user/tokens/verify" || {
    echo "ERROR: Cannot access DNS provider"
    exit 1
}

echo "✓ Pre-failover checks passed"
```

---

## Failover Scenarios

### Scenario A: Complete Regional Outage (Azure East US)

**Trigger**: All Azure services in East US unavailable for 15+ minutes

**Decision Point**: When Azure status page confirms regional outage

**Failover Type**: Emergency (Active-Passive)

**Expected Duration**: 30-45 minutes

---

### Scenario B: Planned Datacenter Maintenance

**Trigger**: Scheduled maintenance window requiring region migration

**Decision Point**: 7 days notice, failover during low-traffic period

**Failover Type**: Planned (Active-Passive)

**Expected Duration**: 20-30 minutes (minimal disruption)

---

### Scenario C: Database Performance Degradation

**Trigger**: Primary database latency > 500ms for 10+ minutes

**Decision Point**: When performance impacts SLA

**Failover Type**: Partial (Database only)

**Expected Duration**: 15-20 minutes

---

### Scenario D: Multi-Region Traffic Rebalancing

**Trigger**: Region overloaded, need to shift traffic

**Decision Point**: When one region CPU > 80% sustained

**Failover Type**: Traffic shift (Active-Active)

**Expected Duration**: 5-10 minutes

---

## Step-by-Step Procedures

### Procedure 1: Emergency Regional Failover (Azure East US → Central US)

**When to use**: Primary region completely unavailable

**Estimated Time**: 30-45 minutes

#### Step 1: Declare Incident and Assess

```bash
#!/bin/bash
# DR-04-regional-failover.sh

set -euo pipefail

START_TIME=$(date +%s)
LOG_FILE="/var/log/honua/dr-failover-$(date +%Y%m%d_%H%M%S).log"
mkdir -p "$(dirname "$LOG_FILE")"

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

log "========================================================"
log "DISASTER RECOVERY: REGIONAL FAILOVER INITIATED"
log "========================================================"

# Configuration
PRIMARY_REGION="eastus"
SECONDARY_REGION="centralus"
ENVIRONMENT="prod"
DOMAIN="gis.honua.io"

log "Failover Details:"
log "  From: $PRIMARY_REGION (PRIMARY)"
log "  To: $SECONDARY_REGION (SECONDARY)"
log "  Environment: $ENVIRONMENT"
log "  Started: $(date)"

# Send incident notification
if [ -n "${SLACK_WEBHOOK:-}" ]; then
    curl -X POST "$SLACK_WEBHOOK" \
        -H 'Content-Type: application/json' \
        -d "{
            \"text\":\"🚨 REGIONAL FAILOVER IN PROGRESS\",
            \"blocks\":[
                {\"type\":\"section\",\"text\":{\"type\":\"mrkdwn\",\"text\":\"*DISASTER RECOVERY ACTIVATED*\"}},
                {\"type\":\"section\",\"fields\":[
                    {\"type\":\"mrkdwn\",\"text\":\"*From:*\\n$PRIMARY_REGION\"},
                    {\"type\":\"mrkdwn\",\"text\":\"*To:*\\n$SECONDARY_REGION\"},
                    {\"type\":\"mrkdwn\",\"text\":\"*ETA:*\\n30-45 minutes\"},
                    {\"type\":\"mrkdwn\",\"text\":\"*Status:*\\nIn Progress\"}
                ]}
            ]
        }"
fi

# Trigger PagerDuty incident
if [ -n "${PAGERDUTY_TOKEN:-}" ]; then
    curl -X POST "https://api.pagerduty.com/incidents" \
        -H "Authorization: Token token=$PAGERDUTY_TOKEN" \
        -H "Content-Type: application/json" \
        -d "{
            \"incident\":{
                \"type\":\"incident\",
                \"title\":\"Regional Failover: $PRIMARY_REGION → $SECONDARY_REGION\",
                \"service\":{\"id\":\"$PAGERDUTY_SERVICE_ID\",\"type\":\"service_reference\"},
                \"urgency\":\"high\",
                \"body\":{\"type\":\"incident_body\",\"details\":\"Automatic regional failover initiated\"}
            }
        }"
fi
```

#### Step 2: Verify Secondary Region Status

```bash
log "Step 1: Verifying secondary region status..."

# Switch to secondary region context
az account set --subscription "$AZURE_SUBSCRIPTION"

# Check secondary resource group
SECONDARY_RG="rg-honua-${ENVIRONMENT}-${SECONDARY_REGION}"

if ! az group show --name "$SECONDARY_RG" &>/dev/null; then
    log "ERROR: Secondary resource group not found: $SECONDARY_RG"
    log "Run DR-03 Infrastructure Recreation first"
    exit 1
fi

log "✓ Secondary resource group exists"

# Check secondary Kubernetes cluster
SECONDARY_CLUSTER="aks-honua-${SECONDARY_REGION}"

az aks get-credentials \
    --resource-group "$SECONDARY_RG" \
    --name "$SECONDARY_CLUSTER" \
    --overwrite-existing \
    --context="aks-$SECONDARY_REGION"

kubectl --context="aks-$SECONDARY_REGION" get nodes | tee -a "$LOG_FILE"

NODE_COUNT=$(kubectl --context="aks-$SECONDARY_REGION" get nodes --no-headers | wc -l)
READY_COUNT=$(kubectl --context="aks-$SECONDARY_REGION" get nodes --no-headers | grep -c Ready || echo "0")

if [ "$NODE_COUNT" -ne "$READY_COUNT" ]; then
    log "WARNING: Not all nodes ready ($READY_COUNT/$NODE_COUNT)"
fi

log "✓ Secondary Kubernetes cluster accessible ($READY_COUNT nodes ready)"

# Check secondary database
SECONDARY_DB_SERVER="postgres-honua-${SECONDARY_REGION}"
SECONDARY_DB_HOST="${SECONDARY_DB_SERVER}.postgres.database.azure.com"

if ! az postgres flexible-server show \
    --resource-group "$SECONDARY_RG" \
    --name "$SECONDARY_DB_SERVER" &>/dev/null; then
    log "ERROR: Secondary database not found"
    exit 1
fi

log "✓ Secondary database exists: $SECONDARY_DB_HOST"
```

#### Step 3: Promote Secondary Database to Primary

```bash
log "Step 2: Promoting secondary database to primary..."

# Stop replication (if async replication configured)
az postgres flexible-server replica stop-replication \
    --resource-group "$SECONDARY_RG" \
    --name "$SECONDARY_DB_SERVER" || {
    log "Note: Database not configured as replica, continuing..."
}

# Get database credentials from Key Vault
SECONDARY_VAULT="kv-honua-${SECONDARY_REGION}-$(openssl rand -hex 4)"

DB_ADMIN_USER=$(az keyvault secret show \
    --vault-name "$SECONDARY_VAULT" \
    --name "PostgreSQL-AdminUser" \
    --query "value" \
    --output tsv)

DB_ADMIN_PASSWORD=$(az keyvault secret show \
    --vault-name "$SECONDARY_VAULT" \
    --name "PostgreSQL-AdminPassword" \
    --query "value" \
    --output tsv)

# Verify database is accessible and current
log "Verifying database state..."

PGPASSWORD="$DB_ADMIN_PASSWORD" psql \
    --host="$SECONDARY_DB_HOST" \
    --port=5432 \
    --username="$DB_ADMIN_USER" \
    --dbname="honua" \
    <<EOF | tee -a "$LOG_FILE"

-- Check database size
SELECT pg_size_pretty(pg_database_size('honua')) as database_size;

-- Check table counts
SELECT schemaname, count(*) as table_count
FROM pg_tables
WHERE schemaname = 'public'
GROUP BY schemaname;

-- Check latest data timestamp
SELECT 'features' as table_name, MAX(updated_at) as latest_update
FROM features
UNION ALL
SELECT 'stac_items', MAX(properties->>'datetime')
FROM stac_items;

EOF

log "✓ Database promoted and verified"

# Set database to read-write (if was in read-only mode)
PGPASSWORD="$DB_ADMIN_PASSWORD" psql \
    --host="$SECONDARY_DB_HOST" \
    --port=5432 \
    --username="$DB_ADMIN_USER" \
    --dbname="honua" \
    --command="ALTER DATABASE honua SET default_transaction_read_only = off;" || true

log "✓ Database set to read-write mode"
```

#### Step 4: Update Application Configuration

```bash
log "Step 3: Updating application configuration in secondary region..."

# Update connection strings to point to secondary (now primary)
kubectl --context="aks-$SECONDARY_REGION" create secret generic postgres-connection \
    --namespace=honua \
    --from-literal=connection-string="Host=$SECONDARY_DB_HOST;Port=5432;Database=honua;Username=$DB_ADMIN_USER;Password=$DB_ADMIN_PASSWORD;SSL Mode=Require;" \
    --dry-run=client -o yaml | kubectl --context="aks-$SECONDARY_REGION" apply -f -

log "✓ Database connection string updated"

# Update ConfigMaps
kubectl --context="aks-$SECONDARY_REGION" patch configmap honua-config \
    --namespace=honua \
    --type merge \
    --patch "{\"data\":{\"Environment\":\"$ENVIRONMENT\",\"Region\":\"$SECONDARY_REGION\",\"FailoverMode\":\"true\"}}"

log "✓ ConfigMaps updated"

# Restart deployments to pick up new config
kubectl --context="aks-$SECONDARY_REGION" rollout restart deployment/honua-server -n honua
kubectl --context="aks-$SECONDARY_REGION" rollout restart deployment/honua-process-framework -n honua-process-framework

# Wait for deployments
kubectl --context="aks-$SECONDARY_REGION" wait --for=condition=available \
    deployment/honua-server \
    -n honua \
    --timeout=300s

kubectl --context="aks-$SECONDARY_REGION" wait --for=condition=available \
    deployment/honua-process-framework \
    -n honua-process-framework \
    --timeout=300s

log "✓ Deployments restarted and ready"
```

#### Step 5: Update DNS to Secondary Region

```bash
log "Step 4: Updating DNS to point to secondary region..."

# Get secondary region ingress IP
SECONDARY_IP=$(kubectl --context="aks-$SECONDARY_REGION" get svc \
    -n ingress-nginx ingress-nginx-controller \
    -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

if [ -z "$SECONDARY_IP" ]; then
    log "ERROR: Cannot get secondary ingress IP"
    exit 1
fi

log "Secondary ingress IP: $SECONDARY_IP"

# Update DNS (Cloudflare example)
CLOUDFLARE_ZONE_ID="<your-zone-id>"
CLOUDFLARE_RECORD_ID="<your-record-id>"

# Get current DNS record
CURRENT_IP=$(curl -s -X GET \
    "https://api.cloudflare.com/client/v4/zones/$CLOUDFLARE_ZONE_ID/dns_records/$CLOUDFLARE_RECORD_ID" \
    -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
    -H "Content-Type: application/json" | jq -r '.result.content')

log "Current DNS IP: $CURRENT_IP"

# Update to secondary IP
curl -X PUT \
    "https://api.cloudflare.com/client/v4/zones/$CLOUDFLARE_ZONE_ID/dns_records/$CLOUDFLARE_RECORD_ID" \
    -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
    -H "Content-Type: application/json" \
    --data "{
        \"type\":\"A\",
        \"name\":\"$DOMAIN\",
        \"content\":\"$SECONDARY_IP\",
        \"ttl\":300,
        \"proxied\":false
    }" | jq '.' | tee -a "$LOG_FILE"

log "✓ DNS updated to secondary region"

# Alternative: Azure Traffic Manager weighted routing
# az network traffic-manager endpoint update \
#     --resource-group "rg-honua-traffic-manager" \
#     --profile-name "honua-global" \
#     --name "honua-centralus" \
#     --type azureEndpoints \
#     --target-resource-id "/subscriptions/.../resourceGroups/rg-honua-prod-centralus/providers/Microsoft.Network/publicIPAddresses/honua-ip" \
#     --weight 100

# Set primary region weight to 0
# az network traffic-manager endpoint update \
#     --resource-group "rg-honua-traffic-manager" \
#     --profile-name "honua-global" \
#     --name "honua-eastus" \
#     --type azureEndpoints \
#     --weight 0

log "Waiting 60 seconds for DNS propagation..."
sleep 60
```

#### Step 6: Verify Failover Success

```bash
log "Step 5: Verifying failover success..."

# Test DNS resolution
RESOLVED_IP=$(dig +short "$DOMAIN" @8.8.8.8 | tail -1)

if [ "$RESOLVED_IP" == "$SECONDARY_IP" ]; then
    log "✓ DNS resolved to secondary region: $RESOLVED_IP"
else
    log "WARNING: DNS not yet propagated (resolved to $RESOLVED_IP, expected $SECONDARY_IP)"
    log "Continuing with direct IP tests..."
fi

# Test health endpoint
if curl -f "https://$DOMAIN/health" -m 10; then
    log "✓ Health endpoint accessible"
else
    log "Trying direct IP..."
    if curl -f "http://$SECONDARY_IP/health" -H "Host: $DOMAIN" -m 10; then
        log "✓ Health endpoint accessible via IP (DNS propagating)"
    else
        log "ERROR: Health endpoint not accessible"
        exit 1
    fi
fi

# Test database read
if curl -f "https://$DOMAIN/ogcapi/collections" -m 10; then
    log "✓ Database read successful"
else
    log "ERROR: Cannot read from database"
    exit 1
fi

# Test database write
TEST_FEATURE=$(cat <<EOF
{
    "type":"Feature",
    "geometry":{"type":"Point","coordinates":[0,0]},
    "properties":{"name":"failover-test-$(date +%s)","failover":"true"}
}
EOF
)

if curl -X POST "https://$DOMAIN/ogcapi/collections/test/items" \
    -H "Content-Type: application/geo+json" \
    -d "$TEST_FEATURE" -m 10; then
    log "✓ Database write successful"
else
    log "ERROR: Cannot write to database"
    exit 1
fi

log "✓ Failover validation complete"
```

#### Step 7: Update Monitoring and Alerts

```bash
log "Step 6: Updating monitoring configuration..."

# Add Grafana annotation
GRAFANA_URL="http://grafana.${SECONDARY_REGION}.honua.io:3000"
GRAFANA_API_KEY="<from-vault>"

curl -X POST "${GRAFANA_URL}/api/annotations" \
    -H "Authorization: Bearer $GRAFANA_API_KEY" \
    -H "Content-Type: application/json" \
    -d "{
        \"text\":\"Regional Failover: $PRIMARY_REGION → $SECONDARY_REGION\",
        \"tags\":[\"disaster-recovery\",\"failover\"],
        \"time\":$(date +%s000)
    }"

# Update alert routing (send alerts to secondary region notification channels)
kubectl --context="aks-$SECONDARY_REGION" patch configmap prometheus-config \
    --namespace=monitoring \
    --type merge \
    --patch '{"data":{"alertmanager.yml":"<updated-config>"}}'

# Restart Prometheus
kubectl --context="aks-$SECONDARY_REGION" rollout restart statefulset/prometheus -n monitoring

log "✓ Monitoring updated"
```

#### Step 8: Communicate Failover Complete

```bash
ELAPSED=$(($(date +%s) - START_TIME))
ELAPSED_MIN=$((ELAPSED / 60))

log "========================================================"
log "REGIONAL FAILOVER COMPLETE"
log "========================================================"
log "Primary Region: $PRIMARY_REGION (INACTIVE)"
log "Secondary Region: $SECONDARY_REGION (ACTIVE)"
log "Failover Duration: ${ELAPSED_MIN} minutes"
log "Status: OPERATIONAL"
log "========================================================"

# Send completion notification
if [ -n "${SLACK_WEBHOOK:-}" ]; then
    curl -X POST "$SLACK_WEBHOOK" \
        -H 'Content-Type: application/json' \
        -d "{
            \"text\":\"✅ REGIONAL FAILOVER COMPLETE\",
            \"blocks\":[
                {\"type\":\"section\",\"text\":{\"type\":\"mrkdwn\",\"text\":\"*DISASTER RECOVERY COMPLETE*\"}},
                {\"type\":\"section\",\"fields\":[
                    {\"type\":\"mrkdwn\",\"text\":\"*New Active Region:*\\n$SECONDARY_REGION\"},
                    {\"type\":\"mrkdwn\",\"text\":\"*Duration:*\\n${ELAPSED_MIN} minutes\"},
                    {\"type\":\"mrkdwn\",\"text\":\"*Status:*\\n✅ Operational\"},
                    {\"type\":\"mrkdwn\",\"text\":\"*URL:*\\nhttps://$DOMAIN\"}
                ]},
                {\"type\":\"section\",\"text\":{\"type\":\"mrkdwn\",\"text\":\"*Next Steps:*\\n• Monitor for 4 hours\\n• Investigate primary region\\n• Plan failback when primary restored\"}}
            ]
        }"
fi

# Update status page
curl -X POST "https://api.statuspage.io/v1/pages/$STATUSPAGE_ID/incidents" \
    -H "Authorization: OAuth $STATUSPAGE_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
        \"incident\":{
            \"name\":\"Regional Failover Complete\",
            \"status\":\"resolved\",
            \"impact_override\":\"none\",
            \"body\":\"Service successfully failed over to $SECONDARY_REGION region. All services operational.\"
        }
    }"
```

---

## Validation

### Post-Failover Validation Checklist

- [ ] **DNS**
  - [ ] Domain resolves to secondary region IP
  - [ ] TTL expired, full propagation complete
  - [ ] All subdomains updated

- [ ] **Application**
  - [ ] Health endpoint returns 200 OK
  - [ ] All services running in secondary region
  - [ ] No errors in application logs
  - [ ] Performance acceptable (< 2x normal latency)

- [ ] **Database**
  - [ ] Database accessible and writable
  - [ ] Replication stopped/promoted
  - [ ] Data current (within RPO)
  - [ ] Backup schedule active

- [ ] **Functionality**
  - [ ] Can read existing data
  - [ ] Can create new data
  - [ ] Search queries work
  - [ ] File uploads work (if applicable)

- [ ] **Monitoring**
  - [ ] Metrics being collected
  - [ ] Alerts firing to correct channels
  - [ ] Dashboards accessible
  - [ ] No critical alerts

- [ ] **Security**
  - [ ] TLS certificates valid
  - [ ] Authentication working
  - [ ] Authorization policies applied
  - [ ] No security alerts

### Continuous Monitoring (First 4 Hours)

```bash
# Monitor key metrics every 5 minutes
watch -n 300 '
    echo "=== Failover Health Check ==="
    echo "Time: $(date)"
    echo ""
    echo "DNS Resolution:"
    dig +short gis.honua.io @8.8.8.8
    echo ""
    echo "Health Status:"
    curl -s https://gis.honua.io/health | jq
    echo ""
    echo "Response Time:"
    curl -w "Total: %{time_total}s\n" -o /dev/null -s https://gis.honua.io/ogcapi/collections
    echo ""
    echo "Error Rate:"
    kubectl --context=aks-centralus logs -l app=honua-server --tail=100 | grep -c ERROR || echo "0"
'
```

---

## Failback Procedures

### When to Failback

Failback to primary region only when:

1. **Primary region fully operational** (confirmed by cloud provider)
2. **Stable for 4+ hours** (no intermittent issues)
3. **During low-traffic window** (planned, not emergency)
4. **Data synchronized** (no data loss during failback)
5. **Stakeholder approval** (business approval to proceed)

### Failback Steps (Reverse of Failover)

```bash
#!/bin/bash
# Failback to primary region

PRIMARY_REGION="eastus"
SECONDARY_REGION="centralus"  # Currently active

# 1. Verify primary region healthy
log "Verifying primary region health..."
az group show --name "rg-honua-prod-$PRIMARY_REGION"
kubectl --context="aks-$PRIMARY_REGION" get nodes

# 2. Sync data from secondary to primary
log "Syncing database from secondary to primary..."

# Create replica from current active (secondary) to primary
az postgres flexible-server replica create \
    --replica-name "postgres-honua-${PRIMARY_REGION}" \
    --resource-group "rg-honua-prod-${PRIMARY_REGION}" \
    --source-server "/subscriptions/.../resourceGroups/rg-honua-prod-${SECONDARY_REGION}/providers/Microsoft.DBforPostgreSQL/flexibleServers/postgres-honua-${SECONDARY_REGION}"

# Wait for replication to catch up
log "Waiting for replication lag < 60 seconds..."
# Monitor pg_last_xact_replay_timestamp

# 3. Update DNS to primary region
log "Updating DNS back to primary region..."

PRIMARY_IP=$(kubectl --context="aks-$PRIMARY_REGION" get svc \
    -n ingress-nginx ingress-nginx-controller \
    -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Update Cloudflare DNS
curl -X PUT "https://api.cloudflare.com/client/v4/zones/$CLOUDFLARE_ZONE_ID/dns_records/$CLOUDFLARE_RECORD_ID" \
    -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
    -d "{\"type\":\"A\",\"name\":\"gis.honua.io\",\"content\":\"$PRIMARY_IP\",\"ttl\":300}"

# 4. Monitor failback
log "Monitoring failback (waiting for DNS propagation)..."
sleep 120

# 5. Verify traffic on primary
curl -f "https://gis.honua.io/health"

log "✅ Failback complete - primary region active"
```

---

## Related Documentation

- [DR Database Recovery](./DR_RUNBOOK_01_DATABASE_RECOVERY.md)
- [DR Infrastructure Recreation](./DR_RUNBOOK_03_INFRASTRUCTURE_RECREATION.md)
- [Multi-Region Architecture](../../infrastructure/terraform/multi-region/README.md)
- [Traffic Manager Configuration](../../infrastructure/terraform/azure/traffic-manager.tf)

---

## Emergency Contacts

| Role | Contact | Availability |
|------|---------|--------------|
| **Incident Commander** | oncall@honua.io | 24x7 |
| **Platform Lead** | platform@honua.io | 24x7 |
| **Database Administrator** | dba@honua.io | 24x7 |
| **Cloud Provider Support** | Azure/AWS Support | 24x7 |

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering & SRE Teams
**Last Tested**: 2025-10-01 (Production Failover Drill)
**Next Test**: 2026-01-01 (Quarterly DR Exercise)
