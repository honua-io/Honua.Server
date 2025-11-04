# Honua Process Framework - Deployment Guide

**Last Updated**: 2025-10-17
**Status**: Production Ready
**Version**: 1.0

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Configuration Guide](#configuration-guide)
- [Deployment Scenarios](#deployment-scenarios)
- [Redis Setup](#redis-setup)
- [Monitoring Setup](#monitoring-setup)
- [TLS/SSL Configuration](#tlsssl-configuration)
- [Scaling Considerations](#scaling-considerations)
- [Troubleshooting](#troubleshooting)

## Overview

The Honua Process Framework provides stateful, event-driven workflow orchestration for long-running operations like deployments, upgrades, metadata processing, and benchmarking. This guide covers deploying and configuring the Process Framework in various environments.

**Key Features**:
- Stateful workflow execution with pause/resume capabilities
- Redis-backed state persistence for distributed environments
- OpenTelemetry instrumentation for full observability
- Support for multiple LLM providers (Azure OpenAI, OpenAI, Anthropic)
- Event-driven architecture with automatic retry and rollback

**Architecture**:
```
┌─────────────────┐
│   CLI/API       │
│   (Process      │
│    Trigger)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────┐
│ Process Runtime │────▶│    Redis     │
│  (SK Process    │     │  (State      │
│   Framework)    │     │   Store)     │
└────────┬────────┘     └──────────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────┐
│  Process Steps  │────▶│  LLM APIs    │
│  (Deployment,   │     │  (Azure/     │
│   Upgrade, etc) │     │   OpenAI)    │
└────────┬────────┘     └──────────────┘
         │
         ▼
┌─────────────────┐
│  Observability  │
│  (Prometheus,   │
│   Grafana)      │
└─────────────────┘
```

## Prerequisites

### Required Components

#### 1. .NET 9 SDK
```bash
# Install .NET 9 SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0

# Verify installation
dotnet --version
# Expected: 9.0.0 or higher
```

#### 2. Redis (for production deployments)
- **Minimum Version**: Redis 6.0+
- **Recommended Version**: Redis 7.2+
- **Required Features**:
  - String operations (GET, SET, SETEX)
  - Set operations (SADD, SMEMBERS, SREM)
  - Expiration (TTL support)

See [Redis Setup](#redis-setup) for detailed installation options.

#### 3. LLM API Access
At least one of the following:
- **Azure OpenAI**: Requires Azure subscription, deployed models (GPT-4, GPT-4o)
- **OpenAI**: Requires API key, access to GPT-4 models
- **Anthropic**: Requires API key, access to Claude models (optional fallback)

#### 4. PostgreSQL (for main Honua application)
- **Version**: PostgreSQL 14+ with PostGIS 3.3+
- **Purpose**: Geospatial data storage (separate from Process Framework state)

### Optional Components

#### 1. Prometheus + Grafana
For metrics visualization and alerting. See [Monitoring Setup](#monitoring-setup).

#### 2. Jaeger/Tempo
For distributed tracing (already integrated via OpenTelemetry).

#### 3. Docker/Kubernetes
For containerized deployments.

## Configuration Guide

### appsettings.json Structure

The Process Framework configuration is split across several sections in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.SemanticKernel": "Information",
      "Honua.Cli.AI.Services.Processes": "Debug"
    }
  },

  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379,ssl=false,abortConnect=false",
    "KeyPrefix": "honua:process:",
    "TtlSeconds": 86400,
    "ValidateConnectionOnStartup": true,
    "ConnectTimeoutMs": 5000,
    "SyncTimeoutMs": 1000
  },

  "LlmProvider": {
    "Provider": "Azure",
    "FallbackProvider": "OpenAI",
    "DefaultTemperature": 0.2,
    "DefaultMaxTokens": 4000,
    "Azure": {
      "EndpointUrl": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key-here",
      "DeploymentName": "gpt-4o",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "DefaultModel": "gpt-4o"
    },
    "OpenAI": {
      "ApiKey": "your-openai-api-key",
      "DefaultModel": "gpt-4",
      "OrganizationId": null
    }
  },

  "ProcessFramework": {
    "MaxConcurrentProcesses": 10,
    "DefaultTimeoutMinutes": 60,
    "EnableAutoRetry": true,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "EnablePauseResume": true,
    "StateCheckpointIntervalSeconds": 30
  },

  "OpenTelemetry": {
    "Metrics": {
      "Enabled": true,
      "PrometheusPort": 9090
    },
    "Tracing": {
      "Enabled": true,
      "OtlpEndpoint": "http://localhost:4317",
      "SamplingRatio": 1.0
    }
  }
}
```

### Configuration Sections Explained

#### Redis Configuration

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Enable Redis for state storage. If false, uses in-memory storage (dev only) |
| `ConnectionString` | string | null | Redis connection string. Format: `host:port,password=xxx,ssl=true` |
| `KeyPrefix` | string | `honua:process:` | Prefix for all Redis keys to avoid collisions |
| `TtlSeconds` | int | 86400 | Time-to-live for process state (24 hours default) |
| `ValidateConnectionOnStartup` | bool | true | Test Redis connection on app startup |
| `ConnectTimeoutMs` | int | 5000 | Connection timeout in milliseconds |
| `SyncTimeoutMs` | int | 1000 | Synchronous operation timeout |

**Connection String Examples**:
```bash
# Local development (no auth, no SSL)
localhost:6379,ssl=false,abortConnect=false

# Production with password and SSL
redis.example.com:6380,password=MySecurePassword123,ssl=true,abortConnect=false

# Azure Cache for Redis
mycache.redis.cache.windows.net:6380,password=AccessKey,ssl=true,abortConnect=false

# AWS ElastiCache (with encryption in transit)
master.mycluster.abc123.use1.cache.amazonaws.com:6379,password=MyToken,ssl=true,abortConnect=false

# Redis Cluster (multiple endpoints)
node1:6379,node2:6379,node3:6379,password=xxx,ssl=true,abortConnect=false
```

#### LLM Provider Configuration

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Provider` | string | Azure | Primary LLM provider (Azure, OpenAI, Anthropic) |
| `FallbackProvider` | string | null | Fallback if primary fails |
| `DefaultTemperature` | float | 0.2 | Temperature for LLM generation (0.0-1.0) |
| `DefaultMaxTokens` | int | 4000 | Max tokens per LLM response |

**Azure-specific options**:
- `EndpointUrl`: Azure OpenAI endpoint (e.g., `https://myresource.openai.azure.com/`)
- `ApiKey`: Azure OpenAI API key (consider using Azure Key Vault in production)
- `DeploymentName`: Deployment name for chat completions (e.g., `gpt-4o`)
- `EmbeddingDeploymentName`: Deployment name for embeddings

**OpenAI-specific options**:
- `ApiKey`: OpenAI API key
- `DefaultModel`: Model name (e.g., `gpt-4`, `gpt-4-turbo`)
- `OrganizationId`: Optional organization ID

#### Process Framework Configuration

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MaxConcurrentProcesses` | int | 10 | Max processes running simultaneously |
| `DefaultTimeoutMinutes` | int | 60 | Default timeout for process execution |
| `EnableAutoRetry` | bool | true | Auto-retry failed steps |
| `MaxRetryAttempts` | int | 3 | Max retry attempts per step |
| `RetryDelaySeconds` | int | 5 | Delay between retries |
| `EnablePauseResume` | bool | true | Allow pausing/resuming processes |
| `StateCheckpointIntervalSeconds` | int | 30 | How often to checkpoint state |

### Environment-Specific Configuration

Create separate configuration files for each environment:

#### Development (`appsettings.Development.json`)
```json
{
  "Redis": {
    "Enabled": false
  },
  "LlmProvider": {
    "Provider": "Mock",
    "DefaultTemperature": 0.0
  },
  "ProcessFramework": {
    "MaxConcurrentProcesses": 2,
    "DefaultTimeoutMinutes": 10
  },
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 1.0
    }
  }
}
```

#### Staging (`appsettings.Staging.json`)
```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "staging-redis.internal:6379,ssl=true,password=${REDIS_PASSWORD}"
  },
  "LlmProvider": {
    "Provider": "Azure",
    "Azure": {
      "EndpointUrl": "https://staging-openai.openai.azure.com/",
      "ApiKey": "${AZURE_OPENAI_KEY}"
    }
  },
  "ProcessFramework": {
    "MaxConcurrentProcesses": 5
  },
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 0.1
    }
  }
}
```

#### Production (`appsettings.Production.json`)
```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "prod-redis-cluster.internal:6379,ssl=true,password=${REDIS_PASSWORD}",
    "TtlSeconds": 604800,
    "ValidateConnectionOnStartup": true
  },
  "LlmProvider": {
    "Provider": "Azure",
    "FallbackProvider": "OpenAI",
    "Azure": {
      "EndpointUrl": "${AZURE_OPENAI_ENDPOINT}",
      "ApiKey": "${AZURE_OPENAI_KEY}"
    }
  },
  "ProcessFramework": {
    "MaxConcurrentProcesses": 20,
    "DefaultTimeoutMinutes": 120,
    "EnableAutoRetry": true,
    "MaxRetryAttempts": 5
  },
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 0.01
    }
  }
}
```

### User Secrets (Development)

For local development, use .NET User Secrets instead of storing sensitive data in appsettings:

```bash
# Initialize user secrets for the project
cd src/Honua.Cli.AI
dotnet user-secrets init

# Set Redis connection string
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379"

# Set Azure OpenAI credentials
dotnet user-secrets set "LlmProvider:Azure:EndpointUrl" "https://myresource.openai.azure.com/"
dotnet user-secrets set "LlmProvider:Azure:ApiKey" "your-api-key-here"

# Set OpenAI credentials (fallback)
dotnet user-secrets set "LlmProvider:OpenAI:ApiKey" "sk-your-openai-key"

# List all secrets
dotnet user-secrets list
```

### Environment Variables

Override configuration via environment variables (useful for Docker/Kubernetes):

```bash
# Redis
export Redis__Enabled=true
export Redis__ConnectionString="redis:6379,ssl=false"

# LLM Provider
export LlmProvider__Provider=Azure
export LlmProvider__Azure__EndpointUrl="https://myresource.openai.azure.com/"
export LlmProvider__Azure__ApiKey="your-key"

# Process Framework
export ProcessFramework__MaxConcurrentProcesses=20
```

**Note**: Use double underscores (`__`) to represent nested configuration sections.

## Deployment Scenarios

### Scenario 1: Local Development (No Redis)

**Use Case**: Development and testing on local machine

**Configuration**:
```json
{
  "Redis": {
    "Enabled": false
  },
  "LlmProvider": {
    "Provider": "Mock"
  }
}
```

**Setup**:
```bash
# Clone repository
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO

# Restore dependencies
dotnet restore

# Run CLI
cd src/Honua.Cli.AI
dotnet run -- process list
```

**Limitations**:
- Process state lost on restart (no persistence)
- No distributed execution
- Single instance only

### Scenario 2: Docker Compose (Development/Staging)

**Use Case**: Multi-container development environment with Redis

**File**: `docker-compose.process-framework.yml`
```yaml
version: '3.8'

services:
  redis:
    image: redis:7.2-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  honua-process-framework:
    build:
      context: .
      dockerfile: src/Honua.Cli.AI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Redis__Enabled=true
      - Redis__ConnectionString=redis:6379,ssl=false,abortConnect=false
      - LlmProvider__Provider=Azure
      - LlmProvider__Azure__EndpointUrl=${AZURE_OPENAI_ENDPOINT}
      - LlmProvider__Azure__ApiKey=${AZURE_OPENAI_KEY}
    depends_on:
      redis:
        condition: service_healthy
    ports:
      - "9090:9090"  # Prometheus metrics

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9091:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources

volumes:
  redis-data:
  prometheus-data:
  grafana-data:
```

**Start services**:
```bash
# Create .env file with secrets
cat > .env <<EOF
AZURE_OPENAI_ENDPOINT=https://myresource.openai.azure.com/
AZURE_OPENAI_KEY=your-key-here
EOF

# Start all services
docker-compose -f docker-compose.process-framework.yml up -d

# Check health
docker-compose ps

# View logs
docker-compose logs -f honua-process-framework

# Stop services
docker-compose down
```

### Scenario 3: Kubernetes (Production)

**Use Case**: Production deployment with high availability

**Files**:

`k8s/namespace.yaml`:
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: honua-process-framework
```

`k8s/redis-deployment.yaml`:
```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: redis
  namespace: honua-process-framework
spec:
  serviceName: redis
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:7.2-alpine
        ports:
        - containerPort: 6379
          name: redis
        volumeMounts:
        - name: redis-data
          mountPath: /data
        command:
        - redis-server
        - --appendonly
        - "yes"
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "1Gi"
            cpu: "500m"
  volumeClaimTemplates:
  - metadata:
      name: redis-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 10Gi
---
apiVersion: v1
kind: Service
metadata:
  name: redis
  namespace: honua-process-framework
spec:
  ports:
  - port: 6379
    targetPort: 6379
  selector:
    app: redis
```

`k8s/process-framework-deployment.yaml`:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-process-framework
  namespace: honua-process-framework
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-process-framework
  template:
    metadata:
      labels:
        app: honua-process-framework
    spec:
      containers:
      - name: honua-cli
        image: honua/cli-ai:latest
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Redis__Enabled
          value: "true"
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: redis-secret
              key: connection-string
        - name: LlmProvider__Azure__EndpointUrl
          valueFrom:
            secretKeyRef:
              name: azure-openai-secret
              key: endpoint-url
        - name: LlmProvider__Azure__ApiKey
          valueFrom:
            secretKeyRef:
              name: azure-openai-secret
              key: api-key
        ports:
        - containerPort: 9090
          name: metrics
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /metrics
            port: 9090
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /metrics
            port: 9090
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: honua-process-framework
  namespace: honua-process-framework
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "9090"
spec:
  selector:
    app: honua-process-framework
  ports:
  - port: 9090
    targetPort: 9090
    name: metrics
```

`k8s/secrets.yaml`:
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: redis-secret
  namespace: honua-process-framework
type: Opaque
stringData:
  connection-string: "redis:6379,ssl=false,abortConnect=false"
---
apiVersion: v1
kind: Secret
metadata:
  name: azure-openai-secret
  namespace: honua-process-framework
type: Opaque
stringData:
  endpoint-url: "https://myresource.openai.azure.com/"
  api-key: "your-api-key-here"
```

**Deploy to Kubernetes**:
```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Create secrets (edit secrets.yaml first!)
kubectl apply -f k8s/secrets.yaml

# Deploy Redis
kubectl apply -f k8s/redis-deployment.yaml

# Deploy Process Framework
kubectl apply -f k8s/process-framework-deployment.yaml

# Check deployment status
kubectl get pods -n honua-process-framework

# View logs
kubectl logs -f deployment/honua-process-framework -n honua-process-framework

# Scale deployment
kubectl scale deployment/honua-process-framework --replicas=5 -n honua-process-framework
```

### Scenario 4: Azure Container Apps

**Use Case**: Serverless container deployment on Azure

**Setup**:
```bash
# Variables
RESOURCE_GROUP="honua-process-framework"
LOCATION="eastus"
CONTAINER_APP_ENV="honua-env"
REDIS_NAME="honua-redis"
APP_NAME="honua-process-framework"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create Azure Cache for Redis
az redis create \
  --name $REDIS_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Basic \
  --vm-size c0 \
  --enable-non-ssl-port false

# Get Redis connection string
REDIS_KEY=$(az redis list-keys --name $REDIS_NAME --resource-group $RESOURCE_GROUP --query primaryKey -o tsv)
REDIS_HOST=$(az redis show --name $REDIS_NAME --resource-group $RESOURCE_GROUP --query hostName -o tsv)
REDIS_CONN_STRING="$REDIS_HOST:6380,password=$REDIS_KEY,ssl=true,abortConnect=false"

# Create Container Apps environment
az containerapp env create \
  --name $CONTAINER_APP_ENV \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# Deploy container app
az containerapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINER_APP_ENV \
  --image honua/cli-ai:latest \
  --cpu 1.0 \
  --memory 2.0Gi \
  --min-replicas 1 \
  --max-replicas 10 \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "Redis__Enabled=true" \
    "Redis__ConnectionString=$REDIS_CONN_STRING" \
  --secrets \
    "azure-openai-key=$AZURE_OPENAI_KEY" \
  --ingress external \
  --target-port 9090

# Get app URL
az containerapp show --name $APP_NAME --resource-group $RESOURCE_GROUP --query properties.configuration.ingress.fqdn -o tsv
```

## Redis Setup

### Option 1: Local Redis (Development)

#### Docker
```bash
# Start Redis with persistence
docker run -d \
  --name redis \
  -p 6379:6379 \
  -v redis-data:/data \
  redis:7.2-alpine \
  redis-server --appendonly yes

# Test connection
docker exec -it redis redis-cli ping
# Expected: PONG

# Monitor commands
docker exec -it redis redis-cli monitor
```

#### Native Installation

**Ubuntu/Debian**:
```bash
sudo apt update
sudo apt install redis-server

# Start Redis
sudo systemctl start redis-server
sudo systemctl enable redis-server

# Test
redis-cli ping
```

**macOS**:
```bash
brew install redis

# Start Redis
brew services start redis

# Test
redis-cli ping
```

**Windows (WSL2)**:
```bash
# Install via WSL2 Ubuntu
sudo apt update
sudo apt install redis-server
sudo service redis-server start

# Test
redis-cli ping
```

### Option 2: Redis Cluster (High Availability)

For production deployments requiring high availability:

**docker-compose-redis-cluster.yml**:
```yaml
version: '3.8'

services:
  redis-node-1:
    image: redis:7.2-alpine
    command: redis-server --cluster-enabled yes --cluster-config-file nodes.conf --cluster-node-timeout 5000 --appendonly yes
    ports:
      - "7001:6379"
    volumes:
      - redis-node-1:/data

  redis-node-2:
    image: redis:7.2-alpine
    command: redis-server --cluster-enabled yes --cluster-config-file nodes.conf --cluster-node-timeout 5000 --appendonly yes
    ports:
      - "7002:6379"
    volumes:
      - redis-node-2:/data

  redis-node-3:
    image: redis:7.2-alpine
    command: redis-server --cluster-enabled yes --cluster-config-file nodes.conf --cluster-node-timeout 5000 --appendonly yes
    ports:
      - "7003:6379"
    volumes:
      - redis-node-3:/data

volumes:
  redis-node-1:
  redis-node-2:
  redis-node-3:
```

**Initialize cluster**:
```bash
# Start nodes
docker-compose -f docker-compose-redis-cluster.yml up -d

# Create cluster
docker exec -it redis-node-1 redis-cli --cluster create \
  127.0.0.1:7001 127.0.0.1:7002 127.0.0.1:7003 \
  --cluster-replicas 0

# Test cluster
docker exec -it redis-node-1 redis-cli -c -p 7001 cluster info
```

### Option 3: Azure Cache for Redis

**Pricing Tiers**:
- **Basic**: Single node, no SLA (dev/test only)
- **Standard**: Primary-replica, 99.9% SLA (recommended for staging)
- **Premium**: Clustering, persistence, VNet support (recommended for production)

**Create via Portal**:
1. Go to Azure Portal → Create Resource → Azure Cache for Redis
2. Select Standard or Premium tier
3. Enable non-SSL port = **No** (always use SSL)
4. Configure virtual network (Premium only)
5. Enable data persistence (Premium only)

**Create via CLI**:
```bash
# Standard tier (recommended for most workloads)
az redis create \
  --name honua-redis-prod \
  --resource-group honua \
  --location eastus \
  --sku Standard \
  --vm-size c1 \
  --enable-non-ssl-port false

# Premium tier with clustering (high availability)
az redis create \
  --name honua-redis-prod \
  --resource-group honua \
  --location eastus \
  --sku Premium \
  --vm-size p1 \
  --shard-count 2 \
  --enable-non-ssl-port false

# Get connection details
az redis show --name honua-redis-prod --resource-group honua
az redis list-keys --name honua-redis-prod --resource-group honua
```

**Connection String**:
```
honua-redis-prod.redis.cache.windows.net:6380,password=AccessKeyHere,ssl=true,abortConnect=false
```

### Option 4: AWS ElastiCache for Redis

**Create via Console**:
1. Go to AWS Console → ElastiCache → Create Redis cluster
2. Select cluster mode (enabled for production)
3. Configure encryption in transit (TLS)
4. Configure encryption at rest
5. Set automatic backups (daily)

**Create via CLI**:
```bash
# Create subnet group
aws elasticache create-cache-subnet-group \
  --cache-subnet-group-name honua-redis-subnet \
  --cache-subnet-group-description "Honua Redis subnet group" \
  --subnet-ids subnet-12345678 subnet-87654321

# Create Redis cluster (non-clustered)
aws elasticache create-cache-cluster \
  --cache-cluster-id honua-redis-prod \
  --engine redis \
  --engine-version 7.0 \
  --cache-node-type cache.r6g.large \
  --num-cache-nodes 1 \
  --cache-subnet-group-name honua-redis-subnet \
  --security-group-ids sg-12345678 \
  --transit-encryption-enabled \
  --auth-token YourStrongAuthToken123

# Get endpoint
aws elasticache describe-cache-clusters \
  --cache-cluster-id honua-redis-prod \
  --show-cache-node-info
```

**Connection String**:
```
honua-redis-prod.abc123.0001.use1.cache.amazonaws.com:6379,password=YourAuthToken,ssl=true,abortConnect=false
```

### Redis Configuration Best Practices

#### 1. Enable Persistence
```bash
# Enable AOF (Append-Only File) for durability
redis-server --appendonly yes --appendfsync everysec
```

#### 2. Set Memory Limits
```bash
# Prevent Redis from consuming all available memory
redis-server --maxmemory 2gb --maxmemory-policy allkeys-lru
```

#### 3. Enable TLS (Production)
```bash
# Generate self-signed certificate (or use proper CA cert)
openssl req -x509 -nodes -newkey rsa:4096 -keyout redis.key -out redis.crt -days 365

# Start Redis with TLS
redis-server \
  --tls-port 6380 \
  --port 0 \
  --tls-cert-file ./redis.crt \
  --tls-key-file ./redis.key \
  --tls-ca-cert-file ./ca.crt
```

#### 4. Configure Authentication
```bash
# Set password in redis.conf
requirepass YourStrongPasswordHere

# Or via command line
redis-server --requirepass YourStrongPasswordHere
```

#### 5. Monitor Redis Health
```bash
# Check memory usage
redis-cli INFO memory

# Check connected clients
redis-cli CLIENT LIST

# Check key count
redis-cli DBSIZE

# Monitor slow queries
redis-cli SLOWLOG GET 10
```

## Monitoring Setup

### Prometheus Configuration

**prometheus.yml**:
```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'honua-process-framework'
    static_configs:
      - targets: ['honua-process-framework:9090']
    metrics_path: '/metrics'

  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']

alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']

rule_files:
  - 'alerts.yml'
```

**alerts.yml**:
```yaml
groups:
  - name: process_framework_alerts
    interval: 30s
    rules:
      - alert: HighProcessFailureRate
        expr: rate(honua_process_failures_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High process failure rate"
          description: "Process failure rate is {{ $value }} per second"

      - alert: ProcessTimeout
        expr: honua_process_timeout_total > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Process timeout detected"
          description: "{{ $value }} processes timed out"

      - alert: RedisConnectionFailure
        expr: redis_up == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Redis connection failed"
          description: "Cannot connect to Redis"

      - alert: HighMemoryUsage
        expr: process_resident_memory_bytes > 2e9
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage"
          description: "Process memory usage is {{ $value | humanize }}B"
```

### Grafana Dashboard

**Import Dashboard JSON**:
```json
{
  "dashboard": {
    "title": "Honua Process Framework",
    "panels": [
      {
        "title": "Active Processes",
        "targets": [
          {
            "expr": "honua_active_processes"
          }
        ]
      },
      {
        "title": "Process Completion Rate",
        "targets": [
          {
            "expr": "rate(honua_process_completed_total[5m])"
          }
        ]
      },
      {
        "title": "Process Failure Rate",
        "targets": [
          {
            "expr": "rate(honua_process_failures_total[5m])"
          }
        ]
      },
      {
        "title": "Average Process Duration",
        "targets": [
          {
            "expr": "avg(honua_process_duration_seconds)"
          }
        ]
      },
      {
        "title": "Redis Operations",
        "targets": [
          {
            "expr": "rate(redis_commands_processed_total[1m])"
          }
        ]
      }
    ]
  }
}
```

### OpenTelemetry Tracing

The Process Framework automatically instruments all process steps with OpenTelemetry.

**View traces in Jaeger**:
```bash
# Start Jaeger all-in-one
docker run -d \
  --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest

# Open Jaeger UI
open http://localhost:16686

# Search for "DeploymentProcess" traces
```

**Configure OTLP exporter**:
```json
{
  "OpenTelemetry": {
    "Tracing": {
      "Enabled": true,
      "OtlpEndpoint": "http://jaeger:4317",
      "SamplingRatio": 1.0
    }
  }
}
```

## TLS/SSL Configuration

### Redis TLS

#### Self-Signed Certificate (Development)
```bash
# Generate CA key and certificate
openssl genrsa -out ca.key 4096
openssl req -x509 -new -nodes -key ca.key -sha256 -days 1024 -out ca.crt \
  -subj "/C=US/ST=CA/O=Honua/CN=Honua CA"

# Generate Redis server key and CSR
openssl genrsa -out redis.key 4096
openssl req -new -key redis.key -out redis.csr \
  -subj "/C=US/ST=CA/O=Honua/CN=redis.local"

# Sign certificate
openssl x509 -req -in redis.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
  -out redis.crt -days 500 -sha256

# Start Redis with TLS
redis-server \
  --tls-port 6380 \
  --port 0 \
  --tls-cert-file ./redis.crt \
  --tls-key-file ./redis.key \
  --tls-ca-cert-file ./ca.crt \
  --tls-auth-clients no
```

#### Connection String with TLS
```json
{
  "Redis": {
    "ConnectionString": "redis.local:6380,ssl=true,sslHost=redis.local"
  }
}
```

### LLM API TLS

Azure OpenAI and OpenAI APIs use TLS by default. No additional configuration needed.

For self-hosted LLM endpoints:
```json
{
  "LlmProvider": {
    "CustomEndpoint": {
      "BaseUrl": "https://llm.example.com/v1",
      "ApiKey": "your-key",
      "VerifySsl": true,
      "ClientCertificatePath": "/path/to/client.crt",
      "ClientCertificateKeyPath": "/path/to/client.key"
    }
  }
}
```

## Scaling Considerations

### Horizontal Scaling

The Process Framework supports horizontal scaling with Redis as shared state store:

**Scaling Pattern**:
```
┌──────────────┐
│   Load       │
│   Balancer   │
└──────┬───────┘
       │
       ├─────────┬─────────┬─────────┐
       │         │         │         │
   ┌───▼───┐ ┌──▼────┐ ┌──▼────┐ ┌──▼────┐
   │ Pod 1 │ │ Pod 2 │ │ Pod 3 │ │ Pod N │
   └───┬───┘ └───┬───┘ └───┬───┘ └───┬───┘
       │         │         │         │
       └─────────┴─────────┴─────────┘
                    │
              ┌─────▼──────┐
              │   Redis    │
              │  Cluster   │
              └────────────┘
```

**Best Practices**:
1. Use Redis cluster for state storage
2. Set `MaxConcurrentProcesses` based on pod resources
3. Use Kubernetes HPA (Horizontal Pod Autoscaler) based on CPU/memory
4. Distribute process execution evenly across pods

**Kubernetes HPA Example**:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-process-framework-hpa
  namespace: honua-process-framework
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-process-framework
  minReplicas: 3
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  - type: Pods
    pods:
      metric:
        name: honua_active_processes
      target:
        type: AverageValue
        averageValue: "5"
```

### Vertical Scaling

**Resource Recommendations**:

| Concurrent Processes | CPU | Memory | Redis Memory |
|---------------------|-----|--------|--------------|
| 1-5 (dev) | 1 core | 1 GB | 256 MB |
| 5-10 (staging) | 2 cores | 2 GB | 512 MB |
| 10-50 (production) | 4 cores | 4 GB | 2 GB |
| 50-100 (high load) | 8 cores | 8 GB | 4 GB |

**Kubernetes Resource Limits**:
```yaml
resources:
  requests:
    memory: "2Gi"
    cpu: "1000m"
  limits:
    memory: "8Gi"
    cpu: "4000m"
```

### Process Concurrency Tuning

**Configuration**:
```json
{
  "ProcessFramework": {
    "MaxConcurrentProcesses": 20,
    "ProcessQueue": {
      "MaxSize": 100,
      "OverflowPolicy": "Reject"
    }
  }
}
```

**Guidelines**:
- Start with `MaxConcurrentProcesses` = CPU cores × 2
- Monitor CPU and memory usage
- Adjust based on process types (CPU-bound vs I/O-bound)
- For LLM-heavy processes, limit to avoid API rate limits

## Troubleshooting

### Common Issues

#### 1. Redis Connection Failed

**Symptoms**:
```
Error: StackExchange.Redis.RedisConnectionException: It was not possible to connect to the redis server(s)
```

**Solutions**:
```bash
# Check Redis is running
docker ps | grep redis
# OR
redis-cli ping

# Check connection string
echo $Redis__ConnectionString

# Test connection manually
redis-cli -h redis.example.com -p 6379 -a password ping

# Check firewall rules
telnet redis.example.com 6379
```

#### 2. LLM API Rate Limit

**Symptoms**:
```
Error: Rate limit exceeded (429 Too Many Requests)
```

**Solutions**:
```json
{
  "ProcessFramework": {
    "MaxConcurrentProcesses": 5,
    "LlmRateLimit": {
      "RequestsPerMinute": 60,
      "TokensPerMinute": 90000
    }
  }
}
```

#### 3. Process Timeout

**Symptoms**:
```
Warning: Process abc-123 exceeded timeout of 60 minutes
```

**Solutions**:
```json
{
  "ProcessFramework": {
    "DefaultTimeoutMinutes": 120,
    "ProcessTimeouts": {
      "DeploymentProcess": 180,
      "UpgradeProcess": 240
    }
  }
}
```

#### 4. High Memory Usage

**Symptoms**:
- OOMKilled pods in Kubernetes
- Process crashes with OutOfMemoryException

**Solutions**:
```json
{
  "ProcessFramework": {
    "MaxConcurrentProcesses": 10,
    "ProcessStepCache": {
      "MaxSizeMB": 500,
      "EvictionPolicy": "LRU"
    }
  }
}
```

```bash
# Increase Kubernetes memory limits
kubectl set resources deployment/honua-process-framework -n honua-process-framework \
  --limits=memory=8Gi --requests=memory=4Gi
```

### Debug Logging

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Honua.Cli.AI.Services.Processes": "Debug",
      "Microsoft.SemanticKernel": "Trace",
      "StackExchange.Redis": "Debug"
    }
  }
}
```

### Health Checks

**Endpoint**: `GET /health`

**Response**:
```json
{
  "status": "Healthy",
  "checks": {
    "redis": "Healthy",
    "llm_provider": "Healthy",
    "process_runtime": "Healthy"
  },
  "duration": "00:00:00.123"
}
```

**Kubernetes Liveness Probe**:
```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 9090
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

## Next Steps

- [Operations Guide](../operations/PROCESS_FRAMEWORK_OPERATIONS.md) - Daily operations and troubleshooting
- [Runbooks](../operations/RUNBOOKS.md) - Step-by-step incident response procedures
- [Quick Start](../quickstart/PROCESS_FRAMEWORK_QUICKSTART.md) - 5-minute getting started guide
- [Process Framework Design](../process-framework-design.md) - Architecture and design principles

---

**Document Version**: 1.0
**Last Updated**: 2025-10-17
**Maintainer**: Honua Team
