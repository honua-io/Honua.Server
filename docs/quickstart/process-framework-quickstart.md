# Honua Process Framework - Quick Start Guide

**Last Updated**: 2025-10-17
**Status**: Production Ready
**Version**: 1.0

## Table of Contents

- [Overview](#overview)
- [5-Minute Quick Start](#5-minute-quick-start)
- [Running Your First Workflow](#running-your-first-workflow)
- [Testing with Mock LLM](#testing-with-mock-llm)
- [Viewing Metrics and Traces](#viewing-metrics-and-traces)
- [Common First-Run Issues](#common-first-run-issues)
- [Next Steps](#next-steps)

## Overview

This guide will get you up and running with the Honua Process Framework in **5 minutes**. By the end, you'll have:

- ✅ Process Framework running locally
- ✅ Executed your first workflow (deployment process)
- ✅ Viewed process metrics and traces
- ✅ Understanding of basic operations

**Prerequisites**:
- .NET 9 SDK installed
- Docker and Docker Compose installed (optional, for Redis)
- 10 GB free disk space
- Internet connection

## 5-Minute Quick Start

### Option 1: Minimal Setup (In-Memory, No Redis)

**Best for**: Quick testing, development

```bash
# 1. Clone repository
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO

# 2. Navigate to CLI project
cd src/Honua.Cli.AI

# 3. Configure for local development (no Redis, mock LLM)
cat > appsettings.Development.json <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.SemanticKernel": "Information"
    }
  },
  "Redis": {
    "Enabled": false
  },
  "LlmProvider": {
    "Provider": "Mock",
    "DefaultTemperature": 0.0,
    "DefaultMaxTokens": 2000
  },
  "ProcessFramework": {
    "MaxConcurrentProcesses": 2,
    "DefaultTimeoutMinutes": 10
  }
}
EOF

# 4. Run the CLI
dotnet run

# Expected output:
# Honua CLI v1.0.0
# Process Framework initialized (in-memory mode)
# Type 'help' for available commands
```

**Total time**: ~2 minutes

### Option 2: Full Setup (with Redis)

**Best for**: Realistic testing, multi-instance support

```bash
# 1. Clone repository
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO

# 2. Start Redis
docker run -d \
  --name redis \
  -p 6379:6379 \
  redis:7.2-alpine

# 3. Navigate to CLI project
cd src/Honua.Cli.AI

# 4. Configure with Redis
cat > appsettings.Development.json <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,ssl=false,abortConnect=false"
  },
  "LlmProvider": {
    "Provider": "Mock"
  }
}
EOF

# 5. Run the CLI
dotnet run -- process list

# Expected output:
# Active Processes: 0
# Redis connected: ✓
```

**Total time**: ~3 minutes

### Option 3: Docker Compose (Full Stack)

**Best for**: Production-like environment, monitoring included

```bash
# 1. Clone repository
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO

# 2. Create docker-compose file
cat > docker-compose.quickstart.yml <<EOF
version: '3.8'

services:
  redis:
    image: redis:7.2-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s

  process-framework:
    build:
      context: .
      dockerfile: src/Honua.Cli.AI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Redis__Enabled=true
      - Redis__ConnectionString=redis:6379,ssl=false
      - LlmProvider__Provider=Mock
    depends_on:
      redis:
        condition: service_healthy
    ports:
      - "9090:9090"

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9091:9090"
    volumes:
      - ./monitoring/prometheus-quickstart.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
EOF

# 3. Create Prometheus config
mkdir -p monitoring
cat > monitoring/prometheus-quickstart.yml <<EOF
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'process-framework'
    static_configs:
      - targets: ['process-framework:9090']
EOF

# 4. Start all services
docker-compose -f docker-compose.quickstart.yml up -d

# 5. Check status
docker-compose -f docker-compose.quickstart.yml ps

# 6. Access services:
# - Process Framework: http://localhost:9090/metrics
# - Prometheus: http://localhost:9091
# - Grafana: http://localhost:3000 (admin/admin)
```

**Total time**: ~5 minutes

## Running Your First Workflow

### Example 1: Simple Test Process

```bash
# Start a basic test process (completes in ~10 seconds)
dotnet run -- process start test \
  --input "test-data" \
  --dry-run false

# Expected output:
# Process started: test-abc123
# Status: Running
# Progress: 0%
#
# ...waiting...
#
# Status: Completed
# Duration: 12.3 seconds
```

### Example 2: Deployment Process (Simulated)

```bash
# Start a simulated deployment process
dotnet run -- process start deployment \
  --cloud-provider AWS \
  --region us-west-2 \
  --deployment-name honua-test \
  --dry-run true

# Expected output:
# Process started: deployment-def456
# Step 1/8: Validating requirements...
# ✓ Cloud provider: AWS
# ✓ Region: us-west-2
# ✓ Prerequisites met
#
# Step 2/8: Generating infrastructure code...
# ✓ VPC configuration generated
# ✓ Subnet configuration generated
# ✓ Security groups configured
#
# Step 3/8: Reviewing infrastructure... (skipped in dry-run)
#
# Step 4/8: Deploying infrastructure... (simulated)
# [DRY-RUN] Would create: vpc-12345
# [DRY-RUN] Would create: subnet-67890
#
# ...
#
# ✓ Deployment process completed (dry-run)
# Duration: 45.2 seconds
# Resources that would be created: 12
```

### Example 3: Metadata Process

```bash
# Process geospatial metadata
dotnet run -- process start metadata \
  --dataset-path /data/sample-cog.tif \
  --stac-collection test-collection \
  --dry-run true

# Expected output:
# Process started: metadata-ghi789
# Step 1/5: Scanning dataset directory...
# ✓ Found 1 dataset: sample-cog.tif
#
# Step 2/5: Extracting metadata...
# ✓ CRS: EPSG:4326
# ✓ Bounds: [-180, -90, 180, 90]
# ✓ Resolution: 0.01 degrees
# ✓ Bands: 3 (Red, Green, Blue)
#
# Step 3/5: Validating metadata...
# ✓ All metadata valid
#
# Step 4/5: Generating STAC item...
# ✓ STAC item created: sample-cog-item.json
#
# Step 5/5: Publishing STAC item... (simulated)
# [DRY-RUN] Would publish to: https://stac.example.com/collections/test-collection/items/sample-cog
#
# ✓ Metadata process completed (dry-run)
```

## Testing with Mock LLM

The Mock LLM provider simulates LLM responses without calling external APIs. Perfect for:
- Local development
- CI/CD testing
- Avoiding API costs
- Deterministic testing

### Enable Mock LLM

Already enabled in quick start configurations above, but to configure manually:

**appsettings.Development.json**:
```json
{
  "LlmProvider": {
    "Provider": "Mock",
    "Mock": {
      "ResponseDelay": 1000,
      "DefaultResponse": "Mock LLM response: I'll help you with that task.",
      "SimulateErrors": false,
      "ErrorRate": 0.0
    }
  }
}
```

### Test Mock LLM Directly

```bash
# Test mock LLM with CLI
dotnet run -- llm test \
  --prompt "Generate Terraform code for AWS VPC" \
  --max-tokens 1000

# Expected output:
# Using provider: Mock
# Prompt: Generate Terraform code for AWS VPC
#
# Response:
# Mock LLM response: Here's a Terraform configuration for AWS VPC:
#
# resource "aws_vpc" "main" {
#   cidr_block = "10.0.0.0/16"
#   enable_dns_hostnames = true
#   enable_dns_support = true
#   tags = {
#     Name = "main-vpc"
#   }
# }
#
# Tokens used: 145 (estimated)
# Duration: 1.2 seconds
```

### Mock LLM Advanced Configuration

```json
{
  "LlmProvider": {
    "Provider": "Mock",
    "Mock": {
      "ResponseDelay": 500,
      "Responses": {
        "ValidateRequirements": "All requirements validated successfully. Prerequisites met.",
        "GenerateInfrastructure": "resource \"aws_vpc\" \"main\" { cidr_block = \"10.0.0.0/16\" }",
        "ReviewInfrastructure": "Infrastructure code reviewed. No security issues found. Estimated cost: $50/month.",
        "Default": "Mock LLM response for step: {stepName}"
      },
      "SimulateErrors": true,
      "ErrorRate": 0.05,
      "ErrorTypes": [
        "RateLimitError",
        "TimeoutError",
        "InvalidResponseError"
      ]
    }
  }
}
```

## Viewing Metrics and Traces

### CLI Metrics Commands

```bash
# List all active processes
dotnet run -- process list --status Running

# Get process details
dotnet run -- process get <process-id>

# Get process history
dotnet run -- process history --since 24h

# View process metrics
dotnet run -- process metrics

# Expected output:
# Process Metrics (Last 24 Hours)
# ================================
# Total Started: 15
# Completed: 12 (80%)
# Failed: 2 (13%)
# Running: 1 (7%)
#
# By Type:
#   Deployment: 5 (3 completed, 1 failed, 1 running)
#   Metadata: 7 (6 completed, 1 failed)
#   GitOps: 3 (3 completed)
#
# Average Duration:
#   Deployment: 45.2 seconds
#   Metadata: 12.3 seconds
#   GitOps: 8.1 seconds
```

### Prometheus Metrics

Access Prometheus at http://localhost:9091 (if using Docker Compose)

**Key Metrics to Check**:

```promql
# Active processes
honua_active_processes

# Process completion rate
rate(honua_process_completed_total[5m])

# Process failure rate
rate(honua_process_failures_total[5m])

# Average duration
avg(honua_process_duration_seconds)

# LLM token usage
sum(honua_llm_tokens_used_total)
```

**Example Queries**:

```bash
# Query via curl
curl -G http://localhost:9091/api/v1/query \
  --data-urlencode 'query=honua_active_processes' | jq

# Output:
# {
#   "status": "success",
#   "data": {
#     "resultType": "vector",
#     "result": [
#       {
#         "metric": {},
#         "value": [1698765432, "3"]
#       }
#     ]
#   }
# }
# (3 active processes)
```

### Grafana Dashboard

Access Grafana at http://localhost:3000 (admin/admin)

**Quick Setup**:

1. **Add Prometheus Data Source**:
   - Go to Configuration → Data Sources → Add data source
   - Select Prometheus
   - URL: `http://prometheus:9090`
   - Click "Save & Test"

2. **Import Dashboard**:
   - Go to Dashboards → Import
   - Upload `monitoring/grafana-dashboard-process-framework.json` (if available)
   - Or create custom dashboard with panels:
     - Active Processes: `honua_active_processes`
     - Success Rate: `rate(honua_process_completed_total[5m]) / rate(honua_process_started_total[5m])`
     - Process Duration: `avg(honua_process_duration_seconds)`

3. **View Dashboard**:
   - Navigate to Dashboards → Honua Process Framework
   - See real-time metrics, charts, and alerts

### Distributed Tracing (Optional)

If using Jaeger/Tempo for tracing:

```bash
# Start Jaeger (add to docker-compose or run separately)
docker run -d \
  --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest

# Configure OTLP endpoint in appsettings
{
  "OpenTelemetry": {
    "Tracing": {
      "Enabled": true,
      "OtlpEndpoint": "http://localhost:4317",
      "SamplingRatio": 1.0
    }
  }
}

# Run a process
dotnet run -- process start deployment --dry-run true

# View traces in Jaeger UI
open http://localhost:16686

# Search for "DeploymentProcess" to see full trace
```

## Common First-Run Issues

### Issue 1: "Redis connection failed"

**Symptoms**:
```
Error: StackExchange.Redis.RedisConnectionException: It was not possible to connect to the redis server(s)
```

**Solutions**:

```bash
# Option A: Check if Redis is running
docker ps | grep redis

# If not running, start it
docker run -d --name redis -p 6379:6379 redis:7.2-alpine

# Option B: Disable Redis (use in-memory storage)
# Edit appsettings.Development.json:
{
  "Redis": {
    "Enabled": false
  }
}

# Option C: Check connection string
# Make sure it matches your Redis host:
{
  "Redis": {
    "ConnectionString": "localhost:6379,ssl=false,abortConnect=false"
  }
}
```

### Issue 2: "LLM API key not configured"

**Symptoms**:
```
Error: Azure OpenAI API key is required but not configured
```

**Solutions**:

```bash
# Option A: Use Mock LLM (no API key needed)
{
  "LlmProvider": {
    "Provider": "Mock"
  }
}

# Option B: Configure Azure OpenAI
{
  "LlmProvider": {
    "Provider": "Azure",
    "Azure": {
      "EndpointUrl": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key-here",
      "DeploymentName": "gpt-4o"
    }
  }
}

# Option C: Use user secrets (recommended for local dev)
dotnet user-secrets set "LlmProvider:Azure:ApiKey" "your-key-here"
```

### Issue 3: ".NET 9 SDK not found"

**Symptoms**:
```
The specified SDK 'Microsoft.NET.Sdk' was not found
```

**Solutions**:

```bash
# Install .NET 9 SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0

# Verify installation
dotnet --version
# Expected: 9.0.0 or higher

# If multiple versions installed, check global.json
cat global.json
# Should specify version 9.0.0
```

### Issue 4: "Port 9090 already in use"

**Symptoms**:
```
Unable to bind to http://localhost:9090: address already in use
```

**Solutions**:

```bash
# Option A: Find and kill process using port 9090
lsof -ti:9090 | xargs kill -9

# Option B: Change port in appsettings
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:9095"
      }
    }
  }
}

# Option C: Use environment variable
ASPNETCORE_URLS="http://localhost:9095" dotnet run
```

### Issue 5: "Process fails immediately"

**Symptoms**:
```
Process started: test-abc123
Status: Failed
Error: Step 'ValidateRequirements' threw exception
```

**Solutions**:

```bash
# Check detailed logs
dotnet run -- process get test-abc123 --verbose

# Enable debug logging
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Honua.Cli.AI.Services.Processes": "Trace"
    }
  }
}

# Run in dry-run mode to test without side effects
dotnet run -- process start deployment --dry-run true

# Check for missing configuration
dotnet run -- config validate
```

### Issue 6: "Cannot find process framework commands"

**Symptoms**:
```
Unrecognized command: process
```

**Solutions**:

```bash
# Verify Process Framework is initialized
dotnet run -- --version

# Should show:
# Honua CLI v1.0.0
# Process Framework: Enabled

# List available commands
dotnet run -- help

# If process commands missing, check DI registration
# Ensure ProcessFrameworkServiceCollectionExtensions is called in Program.cs
```

## Next Steps

Congratulations! You've successfully set up and run your first Honua Process Framework workflow.

### Learn More

- **[Deployment Guide](../deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md)**: Production deployment scenarios
- **[Operations Guide](../operations/PROCESS_FRAMEWORK_OPERATIONS.md)**: Daily operations and monitoring
- **[Runbooks](../operations/RUNBOOKS.md)**: Incident response procedures
- **[Process Framework Design](../process-framework-design.md)**: Architecture and design patterns

### Try More Examples

```bash
# Example 1: Upgrade process (simulated blue-green deployment)
dotnet run -- process start upgrade \
  --deployment honua-test \
  --from-version 1.0.0 \
  --to-version 2.0.0 \
  --dry-run true

# Example 2: GitOps config deployment
dotnet run -- process start gitops \
  --commit-sha abc123def456 \
  --branch main \
  --dry-run true

# Example 3: Benchmark process
dotnet run -- process start benchmark \
  --deployment honua-test \
  --concurrent-users 100 \
  --duration-seconds 300 \
  --dry-run true

# Example 4: Certificate renewal
dotnet run -- process start cert-renewal \
  --domain example.com \
  --provider letsencrypt \
  --dry-run true
```

### Configure for Production

When you're ready to deploy to production:

1. **Enable Redis**: For state persistence across restarts
2. **Configure Real LLM**: Azure OpenAI or OpenAI
3. **Set Up Monitoring**: Prometheus + Grafana
4. **Configure Alerts**: Critical failure alerts
5. **Enable Backups**: Daily Redis backups
6. **Review Security**: TLS, secrets management

See [Deployment Guide](../deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md) for details.

### Get Help

- **Documentation**: Check the docs in `/docs` directory
- **GitHub Issues**: https://github.com/mikemcdougall/HonuaIO/issues
- **Discussions**: https://github.com/mikemcdougall/HonuaIO/discussions

---

## Quick Reference Card

### Essential Commands

```bash
# List processes
dotnet run -- process list

# Start process
dotnet run -- process start <type> [options]

# Get process status
dotnet run -- process get <process-id>

# Cancel process
dotnet run -- process cancel <process-id>

# View metrics
dotnet run -- process metrics

# View logs
dotnet run -- process logs <process-id>

# Health check
dotnet run -- health

# Configuration
dotnet run -- config show
```

### Configuration Files

- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development overrides
- `appsettings.Production.json`: Production overrides
- User secrets: `dotnet user-secrets list`

### Monitoring Endpoints

- Metrics: http://localhost:9090/metrics
- Health: http://localhost:9090/health
- Prometheus: http://localhost:9091
- Grafana: http://localhost:3000

### Default Ports

- Process Framework: 9090
- Redis: 6379
- Prometheus: 9091
- Grafana: 3000
- Jaeger: 16686

---

**Document Version**: 1.0
**Last Updated**: 2025-10-17
**Maintainer**: Honua Team
