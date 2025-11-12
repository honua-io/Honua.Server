# Observability Migration Guide

## Overview

This guide helps you migrate from legacy observability configurations to the latest Honua Server observability features, including cloud provider support, health checks, and SLI/SLO monitoring.

## What's New

### Version 2.0 Observability Features

| Feature | Legacy | Current | Status |
|---------|--------|---------|--------|
| **Cloud Providers** | Self-hosted only | Azure, AWS, GCP, Self-hosted | ✅ NEW |
| **Health Checks** | Basic /health | 4 specialized checks + K8s probes | ✅ ENHANCED |
| **SLI/SLO Monitoring** | None | Pre-configured recording rules | ✅ NEW |
| **Recording Rules** | None | 60+ pre-computed metrics | ✅ NEW |
| **Alerts** | 15 basic alerts | 35+ comprehensive alerts | ✅ ENHANCED |
| **Metrics** | HTTP only | HTTP, DB, Queue, License, Registry, AI | ✅ ENHANCED |

---

## Migration Paths

### Path 1: Legacy Azure Monitor → Modern Azure Application Insights

**Who**: Users currently using `observability.tracing.exporter: azuremonitor`

**Before:**
```json
{
  "observability": {
    "tracing": {
      "exporter": "azuremonitor",
      "appInsightsConnectionString": "InstrumentationKey=xxx;..."
    }
  }
}
```

**After:**
```json
{
  "observability": {
    "cloudProvider": "azure",
    "azure": {
      "connectionString": "InstrumentationKey=xxx;..."
    }
  }
}
```

**Migration Steps:**

1. **Update configuration** (backward compatible):
   ```bash
   # Option A: Keep using legacy config (deprecated)
   # No action needed, but consider migrating

   # Option B: Migrate to new config (recommended)
   # Update appsettings.Production.json as shown above
   ```

2. **Verify metrics export**:
   ```bash
   # Check Azure Portal → Application Insights → Metrics
   # Look for "honua.http.requests.total" custom metrics
   ```

3. **Update environment variables** (if using):
   ```bash
   # Old
   export observability__tracing__appInsightsConnectionString="..."

   # New
   export observability__cloudProvider=azure
   export APPLICATIONINSIGHTS_CONNECTION_STRING="..."
   ```

**Breaking Changes:** None (legacy config still works but is deprecated)

---

### Path 2: Self-Hosted → AWS CloudWatch

**Who**: Users migrating from self-hosted Prometheus to AWS

**Steps:**

1. **Deploy ADOT Collector** (ECS or EKS):

   **ECS Task Definition:**
   ```json
   {
     "containerDefinitions": [
       {
         "name": "honua-server",
         "environment": [
           {
             "name": "observability__cloudProvider",
             "value": "aws"
           },
           {
             "name": "observability__aws__region",
             "value": "us-east-1"
           },
           {
             "name": "observability__aws__otlpEndpoint",
             "value": "http://localhost:4317"
           }
         ]
       },
       {
         "name": "aws-otel-collector",
         "image": "public.ecr.aws/aws-observability/aws-otel-collector:latest"
       }
     ]
   }
   ```

2. **Update IAM permissions**:
   ```bash
   aws iam attach-role-policy \
     --role-name honua-server-task-role \
     --policy-arn arn:aws:iam::aws:policy/CloudWatchAgentServerPolicy

   aws iam attach-role-policy \
     --role-name honua-server-task-role \
     --policy-arn arn:aws:iam::aws:policy/AWSXRayDaemonWriteAccess
   ```

3. **Configure Grafana for CloudWatch**:
   ```yaml
   apiVersion: 1
   datasources:
     - name: CloudWatch
       type: cloudwatch
       jsonData:
         defaultRegion: us-east-1
         authType: keys
   ```

4. **Migrate dashboards**:
   - Export existing Prometheus dashboards
   - Update queries to use CloudWatch datasource
   - Import into Grafana with CloudWatch datasource

**Cost Considerations:** AWS CloudWatch costs ~$300-1000/month vs. $100-400/month for self-hosted (depending on scale)

---

### Path 3: Self-Hosted → GCP Cloud Monitoring

**Who**: Users migrating to GCP Cloud Monitoring

**Steps:**

1. **Create GCP service account**:
   ```bash
   gcloud iam service-accounts create honua-observability \
     --display-name="Honua Observability"

   gcloud projects add-iam-policy-binding my-project-123456 \
     --member="serviceAccount:honua-observability@my-project-123456.iam.gserviceaccount.com" \
     --role="roles/monitoring.metricWriter"

   gcloud projects add-iam-policy-binding my-project-123456 \
     --member="serviceAccount:honua-observability@my-project-123456.iam.gserviceaccount.com" \
     --role="roles/cloudtrace.agent"
   ```

2. **Deploy OpenTelemetry Collector**:
   ```yaml
   apiVersion: apps/v1
   kind: DaemonSet
   metadata:
     name: otel-collector
   spec:
     template:
       spec:
         serviceAccountName: honua-observability
         containers:
         - name: otel-collector
           image: otel/opentelemetry-collector-contrib:latest
           env:
           - name: GOOGLE_CLOUD_PROJECT
             value: my-project-123456
   ```

3. **Update Honua Server config**:
   ```json
   {
     "observability": {
       "cloudProvider": "gcp",
       "gcp": {
         "projectId": "my-project-123456",
         "otlpEndpoint": "http://otel-collector:4317"
       }
     }
   }
   ```

---

## Enabling New Features

### Feature 1: Health Checks

**Who**: All users (highly recommended for production)

**Before:**
```csharp
// No health checks configured
app.MapHealthChecks("/health");
```

**After:**
```csharp
// In Program.cs
builder.Services.AddHonuaObservability(
    serviceName: "Honua.Server",
    serviceVersion: "1.0.0",
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")
);

app.UseHonuaHealthChecks();
```

**Verification:**
```bash
# Test overall health
curl http://localhost:5000/health

# Test liveness (for K8s)
curl http://localhost:5000/health/live

# Test readiness (for K8s)
curl http://localhost:5000/health/ready
```

**Kubernetes Integration:**
```yaml
apiVersion: v1
kind: Pod
metadata:
  name: honua-server
spec:
  containers:
  - name: honua-server
    livenessProbe:
      httpGet:
        path: /health/live
        port: 5000
      initialDelaySeconds: 30
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 5000
      initialDelaySeconds: 10
      periodSeconds: 5
```

**Breaking Changes:** None

---

### Feature 2: SLI/SLO Monitoring

**Who**: Production deployments tracking service reliability

**Migration Steps:**

1. **Copy recording rules**:
   ```bash
   cp src/Honua.Server.Observability/prometheus/recording-rules.yml /etc/prometheus/
   ```

2. **Update Prometheus configuration**:
   ```yaml
   # prometheus.yml
   rule_files:
     - "alerts.yml"
     - "recording-rules.yml"  # ADD THIS LINE
   ```

3. **Reload Prometheus**:
   ```bash
   curl -X POST http://localhost:9090/-/reload
   ```

4. **Verify recording rules are active**:
   ```bash
   # Check Prometheus UI → Status → Rules
   # Look for "honua_http_sli" group

   # Query a recording rule
   curl 'http://localhost:9090/api/v1/query?query=honua:availability:ratio_5m'
   ```

5. **Import SLO dashboard** (see [SLI/SLO Guide](sli-slo-monitoring.md))

**Breaking Changes:** None

---

### Feature 3: Recording Rules

**Who**: All self-hosted Prometheus users

**Benefits:**
- Faster dashboard loading
- Reduced query load on Prometheus
- Pre-computed SLI metrics

**Migration:**
```bash
# 1. Copy recording rules
cp src/Honua.Server.Observability/prometheus/recording-rules.yml /etc/prometheus/

# 2. Update Prometheus config
cat >> /etc/prometheus/prometheus.yml <<EOF
rule_files:
  - "recording-rules.yml"
EOF

# 3. Reload Prometheus
curl -X POST http://localhost:9090/-/reload

# 4. Verify rules are loaded
curl http://localhost:9090/api/v1/rules | jq
```

**Breaking Changes:** None

---

### Feature 4: Enhanced Alerts

**Who**: All users

**New Alerts:**
- SLO-based alerts (error budget burn rate)
- Health check alerts
- Infrastructure alerts (GC, thread pool)
- Multi-window multi-burn-rate alerts

**Migration:**
```bash
# 1. Backup existing alerts
cp /etc/prometheus/alerts.yml /etc/prometheus/alerts.yml.backup

# 2. Copy new alerts
cp src/Honua.Server.Observability/prometheus/alerts.yml /etc/prometheus/

# 3. Merge custom alerts (if any)
# Edit /etc/prometheus/alerts.yml to add your custom alerts

# 4. Reload Prometheus
curl -X POST http://localhost:9090/-/reload

# 5. Verify alerts loaded
# Prometheus UI → Alerts
```

**Breaking Changes:** None (existing alerts preserved if you merge instead of replace)

---

## Breaking Changes

### NONE

All new features are **backward compatible**. Legacy configurations continue to work but are deprecated.

### Deprecated Configurations

| Deprecated | Replacement | Removal Date |
|------------|-------------|--------------|
| `observability.tracing.exporter: azuremonitor` | `observability.cloudProvider: azure` | TBD (will warn in logs) |
| `observability.tracing.appInsightsConnectionString` | `observability.azure.connectionString` | TBD |

---

## Rollback Procedures

### Rollback Health Checks

If health checks cause issues:

```csharp
// Comment out in Program.cs
// app.UseHonuaHealthChecks();
```

### Rollback SLI/SLO Monitoring

```bash
# Remove recording rules from Prometheus config
sed -i '/recording-rules.yml/d' /etc/prometheus/prometheus.yml

# Reload Prometheus
curl -X POST http://localhost:9090/-/reload
```

### Rollback Cloud Provider

```bash
# Switch back to self-hosted
export observability__cloudProvider=none

# Restart Honua Server
systemctl restart honua-server
```

---

## Testing Migration

### Pre-Migration Checklist

- [ ] Backup current configuration files
- [ ] Document current alert thresholds
- [ ] Export existing Grafana dashboards
- [ ] Test in staging environment first
- [ ] Schedule maintenance window (if needed)

### Post-Migration Verification

1. **Verify metrics collection**:
   ```bash
   curl http://localhost:9090/api/v1/query?query=honua:availability:ratio_5m
   ```

2. **Check health endpoints**:
   ```bash
   curl http://localhost:5000/health
   curl http://localhost:5000/health/live
   curl http://localhost:5000/health/ready
   ```

3. **Verify alerts are firing**:
   ```bash
   # Check Prometheus UI → Alerts
   # Ensure no unexpected alerts
   ```

4. **Test dashboards**:
   ```bash
   # Open Grafana → Dashboards
   # Verify all panels load data
   ```

5. **Monitor for 24 hours**:
   - Watch for missing metrics
   - Check for increased error rates
   - Verify alert accuracy

---

## Migration Timeline

### Recommended Approach

**Week 1: Development Environment**
- Enable health checks
- Deploy recording rules
- Test SLI/SLO metrics
- Update dashboards

**Week 2: Staging Environment**
- Full migration in staging
- Run load tests
- Verify all metrics
- Train team on new features

**Week 3: Production (Phased)**
- Day 1: Enable health checks
- Day 3: Deploy recording rules (read-only)
- Day 5: Enable SLI/SLO alerts (warning only)
- Day 7: Enable all alerts

**Week 4: Optimization**
- Tune alert thresholds
- Optimize recording rules
- Update runbooks
- Document learnings

---

## Common Migration Scenarios

### Scenario 1: Kubernetes Migration

**Goal**: Add health checks for K8s liveness/readiness probes

**Steps:**
1. Add health checks to Honua Server (code change)
2. Update Kubernetes pod spec with probes
3. Deploy with rolling update
4. Verify pods become ready

**Rollback**: Remove probes from pod spec, rollback deployment

---

### Scenario 2: Multi-Region Deployment

**Goal**: Deploy observability across multiple AWS regions

**Steps:**
1. Deploy ADOT Collector in each region
2. Configure each Honua Server instance with regional config
3. Create aggregated CloudWatch dashboard
4. Set up cross-region alerting

**Considerations**:
- Data transfer costs between regions
- Alert aggregation strategy
- Dashboard consolidation

---

### Scenario 3: Hybrid Cloud

**Goal**: Mix of on-premises and cloud deployments

**Options:**

**Option A: Dual-write**
- Self-hosted Prometheus for on-prem
- Cloud provider for cloud deployments
- Federated Prometheus for global view

**Option B: Cloud-based aggregation**
- Forward all metrics to cloud provider
- Single pane of glass
- Higher cost but simpler

---

## Troubleshooting Migration

### Issue: Recording rules not evaluating

**Symptoms**: Queries like `honua:availability:ratio_5m` return no data

**Solutions:**
```bash
# Check Prometheus logs
docker logs prometheus | grep -i "rule"

# Verify rules are loaded
curl http://localhost:9090/api/v1/rules | jq '.data.groups[] | select(.name | contains("honua"))'

# Check rule syntax
promtool check rules /etc/prometheus/recording-rules.yml

# Verify base metrics exist
curl 'http://localhost:9090/api/v1/query?query=honua.http.requests.total'
```

---

### Issue: Health checks failing after migration

**Symptoms**: `/health/ready` returns unhealthy

**Solutions:**
```bash
# Check individual health checks
curl http://localhost:5000/health | jq

# Check database connectivity
psql -h localhost -U honua -d honua -c "SELECT 1"

# Check application logs
docker logs honua-server | grep -i "health"

# Verify connection string
echo $ConnectionStrings__DefaultConnection
```

---

### Issue: Cloud provider not receiving metrics

**Symptoms**: No metrics in Azure/AWS/GCP console

**Solutions:**

**Azure:**
```bash
# Verify connection string
az monitor app-insights component show \
  --app honua-server-prod \
  --resource-group honua-rg \
  --query connectionString

# Check Application Insights ingestion
# Azure Portal → Application Insights → Live Metrics
```

**AWS:**
```bash
# Verify IAM permissions
aws sts get-caller-identity

# Check ADOT Collector logs
docker logs aws-otel-collector

# List CloudWatch metrics
aws cloudwatch list-metrics --namespace Honua
```

**GCP:**
```bash
# Verify service account
gcloud auth list

# Check collector logs
kubectl logs -n monitoring otel-collector-xxxxx

# List custom metrics
gcloud monitoring metrics-descriptors list --filter="metric.type:custom.googleapis.com/honua"
```

---

## Best Practices

### 1. Test in Staging First

Always test migration in a non-production environment:
```bash
# Create staging config
cp appsettings.Production.json appsettings.Staging.json

# Test with staging environment
export ASPNETCORE_ENVIRONMENT=Staging
dotnet run
```

### 2. Phased Rollout

Enable features one at a time:
1. Week 1: Health checks
2. Week 2: Recording rules
3. Week 3: SLI/SLO alerts
4. Week 4: Cloud provider

### 3. Monitor Observability Costs

Set up billing alerts:
- **Azure**: Cost Management → Budgets
- **AWS**: CloudWatch → Billing Alarms
- **GCP**: Billing → Budgets & Alerts

### 4. Document Changes

Create migration runbook:
- Configuration changes made
- Alert threshold adjustments
- Dashboard modifications
- Rollback procedures

---

## Support

### Resources
- [Cloud Provider Setup Guide](cloud-provider-setup.md)
- [SLI/SLO Monitoring Guide](sli-slo-monitoring.md)
- [Deployment Checklist](../deployment/observability-deployment-checklist.md)

### Getting Help
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io
- Email: support@honua.io

---

## Next Steps

After completing migration:

1. **Optimize Alert Thresholds**
   - Review alert history
   - Adjust based on actual performance
   - Reduce false positives

2. **Create Custom Dashboards**
   - Business-specific metrics
   - Team-specific views
   - Executive summaries

3. **Train Team**
   - SLI/SLO interpretation
   - Alert response procedures
   - Dashboard usage

4. **Document Runbooks**
   - Alert response procedures
   - Common troubleshooting steps
   - Escalation paths

5. **Review Quarterly**
   - SLO targets
   - Alert effectiveness
   - Cost optimization
   - Feature usage

---

**Migration Complete!**

You now have comprehensive observability with:
- ✅ Cloud provider integration
- ✅ Production-ready health checks
- ✅ SLI/SLO monitoring
- ✅ Error budget tracking
- ✅ Comprehensive alerting
