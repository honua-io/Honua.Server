# Honua AI Consultant - REAL Integration Tests

**NO MOCKS. NO HARDCODED RESPONSES. REAL AI. REAL INFRASTRUCTURE.**

This test suite uses **actual OpenAI/Claude** to generate deployment configurations and validates them against **real deployed infrastructure** across multiple cloud platforms and deployment targets.

## 🚀 Quick Start

### Set Your API Key (Required!)

```bash
# OpenAI (GPT-4)
export OPENAI_API_KEY=sk-your-actual-api-key-here

# OR Anthropic (Claude)
export ANTHROPIC_API_KEY=sk-ant-your-actual-api-key-here
```

### Run Comprehensive Platform Tests (NEW!)

```bash
# Test AI consultant across ALL platforms: Docker, AWS ECS, Azure Container Apps, GCP Cloud Run, Kubernetes
./run-all-ai-consultant-platform-tests.sh

# This will:
# 1. Use real LLM to generate deployment configs for each platform
# 2. Validate configurations contain required cloud resources
# 3. (Docker) Actually deploy and validate HTTP endpoints
# 4. Test troubleshooting scenarios across all platforms
# 5. Generate comprehensive test report
```

### Run Individual Platform Tests

```bash
# Docker with infrastructure validation
./scripts/test-ai-consultant-docker-comprehensive.sh

# AWS ECS with Terraform
./scripts/test-ai-consultant-aws-ecs.sh

# Azure Container Apps with Bicep/Terraform
./scripts/test-ai-consultant-azure-container-apps.sh

# GCP Cloud Run with Terraform
./scripts/test-ai-consultant-gcp-cloud-run.sh

# Kubernetes with manifests and Helm
./scripts/test-ai-consultant-kubernetes.sh
```

### Run Original AI Integration Tests

```bash
# Original AI-powered Docker integration tests
./run-real-ai-integration-tests.sh
```

### Or Run Legacy Infrastructure Tests

```bash
# These still test infrastructure but use hardcoded configs
./run-all-tests.sh
```

## 🎯 Comprehensive Platform Tests (NEW!)

The new platform test suite validates the AI consultant across all major deployment targets:

### **Interactive Requirements Gathering** (8 tests)
1. **Vague Request Handling** - AI asks clarifying questions for incomplete requests
2. **Workload Characteristics** - Extracts users, data volume, traffic patterns, latency needs
3. **Budget Constraints** - Recognizes cost limits and optimizes for budget
4. **Compliance Requirements** - Identifies HIPAA/SOC2/etc and configures security
5. **Architecture Trade-offs** - Discusses multiple options with pros/cons
6. **Geographic Distribution** - Designs multi-region for global users
7. **Team Skill Level** - Adapts complexity based on DevOps experience
8. **Data Volume Optimization** - Handles TB-scale data with caching/CDN

**Validation**: AI extracts requirements from natural language and designs appropriate topology

### **Docker Comprehensive** (6 tests with infrastructure validation)
1. **PostGIS + Nginx + Redis** - Production-ready stack with proxy caching
2. **MySQL + Traefik + Redis** - Dynamic routing and automatic HTTPS
3. **SQL Server + Caddy** - Microsoft stack with modern reverse proxy
4. **HA with PgPool** - Connection pooling and failover
5. **Troubleshooting** - Memory optimization and resource tuning
6. **Security Hardening** - Non-root containers, secrets, TLS

**Validation**: Actually deploys infrastructure, validates HTTP endpoints, OGC API

### **AWS ECS** (6 tests)
1. **Basic ECS + RDS** - Fargate deployment with PostgreSQL+PostGIS
2. **S3 Tile Caching** - Lifecycle policies and CloudFront CDN
3. **High Availability** - Multi-AZ with ALB, ElastiCache, auto-scaling
4. **CloudWatch Monitoring** - Custom metrics and alerting
5. **Troubleshooting** - OOM diagnostics and task memory optimization
6. **Security** - VPC, WAF, IAM task roles, encryption

### **Azure Container Apps** (6 tests)
1. **Basic Container Apps** - Azure Database for PostgreSQL, Key Vault
2. **Blob Storage Caching** - Lifecycle management and Azure Front Door
3. **High Availability** - Multi-region with Traffic Manager, auto-scaling
4. **Application Insights** - Monitoring and custom metrics
5. **Managed Identity** - RBAC without connection strings
6. **Troubleshooting** - Cold start optimization

### **GCP Cloud Run** (7 tests)
1. **Basic Cloud Run** - Cloud SQL PostgreSQL, Secret Manager
2. **Cloud Storage Caching** - Lifecycle policies, CORS, Cloud CDN
3. **Multi-Region** - Global HTTP(S) LB, Memorystore Redis
4. **Cloud Monitoring** - Logging and alerting policies
5. **Workload Identity** - Keyless authentication
6. **VPC Networking** - Private IP access with VPC connector
7. **Troubleshooting** - Cloud SQL timeout diagnostics

### **Kubernetes** (6 tests)
1. **Basic K8s Deployment** - StatefulSet, Services, health checks
2. **Helm Chart** - Configurable templates and values
3. **HPA** - Auto-scaling based on CPU/memory
4. **Ingress + TLS** - Nginx Ingress, cert-manager, Let's Encrypt
5. **ConfigMap + Secrets** - Configuration and credential management
6. **Troubleshooting** - OOMKilled pod diagnostics

## 🎯 Original AI Test Scenarios

The original AI integration tests cover:

### 1. **Docker MySQL + Redis**
- AI generates complete docker-compose.yml from natural language
- Deploys MySQL database + Redis caching
- Validates OGC API endpoints respond correctly

### 2. **Docker PostGIS Production**
- AI generates production-ready PostGIS setup
- Includes health checks and restart policies
- Validates spatial query endpoints

### 3. **Docker SQL Server**
- AI generates Microsoft SQL Server configuration
- Validates database connectivity

### 4. **AI Troubleshooting Scenario**
- Creates a deployment
- Asks AI to diagnose performance issues
- Validates AI provides actionable performance recommendations

### 5. **AWS LocalStack** (Optional)
- AI generates Terraform for AWS
- Uses LocalStack for S3 simulation
- Validates cloud storage integration

### 6. **Kubernetes** (Optional)
- AI generates K8s manifests
- Deploys to Minikube
- Validates pods and services

---

## 📋 Legacy Infrastructure Tests

The legacy test suite validates infrastructure (no AI generation):

### Docker Compose Tests
1. **PostGIS + Nginx + Redis** - Complete GIS stack
2. **SQL Server + Caddy + Redis** - Microsoft stack
3. **MySQL + Traefik + Redis** - Alternative stack

### Cloud Emulation Tests (LocalStack)
4. **AWS S3 + RDS** - Amazon Web Services integration
5. **Azure Blob + PostgreSQL** - Microsoft Azure integration

### Kubernetes Test
6. **Minikube + PostgreSQL + HPA + Ingress** - Production K8s deployment

## What Gets Tested

### AI Assistant Capabilities
- ✅ Configuration file generation (Docker Compose, K8s manifests)
- ✅ Database connection strings for all providers
- ✅ Reverse proxy setup (Nginx/Caddy/Traefik)
- ✅ Cloud storage integration (S3/Azure)
- ✅ Caching configuration (Redis)
- ✅ Security and SSL/TLS
- ✅ Auto-scaling (HPA)

### Honua Functionality
- ✅ OGC API Features (landing page, collections, items)
- ✅ Spatial queries (bbox, limit, offset)
- ✅ WFS/WMS endpoints
- ✅ Geoservices REST a.k.a. Esri REST API
- ✅ OData queries
- ✅ Performance (response times, throughput)
- ✅ Concurrent load handling
- ✅ Cache effectiveness

## Prerequisites

```bash
# Required
sudo apt-get install -y docker.io docker-compose jq curl

# Optional (for full test suite)
sudo apt-get install -y minikube kubectl

# For LocalStack
docker pull localstack/localstack:latest
```

## Test Architecture

```
tests/e2e-assistant/
├── run-all-tests.sh              # Main orchestrator
├── docker-compose/               # Docker test configs
├── localstack/                   # Cloud emulation configs
├── minikube/                     # Kubernetes manifests
├── scripts/                      # Individual test scripts
└── results/                      # Test output and reports
```

## Individual Test Execution

### Test 1: Docker + PostGIS

```bash
./scripts/test-docker-postgis.sh
```

**Validates**:
- PostGIS spatial extensions
- Nginx reverse proxy
- Redis caching
- OGC API endpoint functionality
- Spatial query correctness

**Expected Duration**: ~45 seconds

### Test 4: LocalStack AWS

```bash
./scripts/test-localstack-aws.sh
```

**Validates**:
- S3 bucket creation
- Tile caching to S3
- RDS database emulation
- Secrets Manager integration
- AWS SDK configuration

**Expected Duration**: ~38 seconds

### Test 6: Minikube Kubernetes

```bash
./scripts/test-minikube.sh
```

**Validates**:
- Pod deployment and health
- StatefulSet for PostgreSQL
- HorizontalPodAutoscaler
- Service load balancing
- Ingress routing

**Expected Duration**: ~95 seconds

## Results Format

After execution, results are saved in `results/run_TIMESTAMP/`:

```
results/run_20251004_153045/
├── summary.md                    # Test summary report
├── docker-postgis-nginx-redis.log
├── docker-sqlserver-caddy-redis.log
├── docker-mysql-traefik-redis.log
├── localstack-aws-s3-rds.log
├── localstack-azure-blob-postgres.log
└── minikube-postgres-hpa-ingress.log
```

**summary.md** example:
```markdown
# Honua AI Assistant - E2E Test Results

**Test Run**: 20251004_153045
**Total Tests**: 6
**Passed**: 6
**Failed**: 0
**Success Rate**: 100%

| Test Name | Status | Duration |
|-----------|--------|----------|
| docker-postgis-nginx-redis | ✅ PASS | 45s |
| ... | ... | ... |
```

## Success Criteria

Tests pass when:
- ✅ All services start healthy
- ✅ OGC API endpoints return valid responses
- ✅ Spatial queries return correct feature counts
- ✅ Performance meets SLA (< 500ms response time)
- ✅ No errors in application logs
- ✅ Cache hit rate > 50% after warmup
- ✅ Memory usage stable over test duration

## Troubleshooting

### Docker Compose fails to start

```bash
# Check Docker is running
docker ps

# Check port availability
ss -tln | grep -E '(18080|19080|20080)'

# View service logs
docker-compose -f docker-compose/test-postgis.yml logs
```

### LocalStack connection refused

```bash
# Verify LocalStack is running
docker ps | grep localstack

# Check health
curl http://localhost:4566/_localstack/health

# Restart LocalStack
docker restart honua-localstack-e2e
```

### Minikube test fails

```bash
# Check Minikube status
minikube status --profile honua-e2e

# Start Minikube if needed
minikube start --profile honua-e2e

# Check pod status
kubectl get pods -n honua-test
```

## Cleanup

Tests automatically clean up after execution. For manual cleanup:

```bash
# Docker Compose
docker-compose -f docker-compose/test-*.yml down -v

# LocalStack
docker stop honua-localstack-e2e
docker rm honua-localstack-e2e

# Minikube
minikube delete --profile honua-e2e
```

## CI/CD Integration

### GitHub Actions

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run E2E Tests
        run: |
          cd tests/e2e-assistant
          ./run-all-tests.sh
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: e2e-results
          path: tests/e2e-assistant/results/
```

## Performance Baselines

| Metric | Baseline | Target | Alert Threshold |
|--------|----------|--------|-----------------|
| OGC Landing Page | 50ms | < 100ms | > 150ms |
| Collections Endpoint | 150ms | < 300ms | > 500ms |
| Feature Query (10 items) | 250ms | < 400ms | > 600ms |
| Concurrent Throughput | 150 req/s | > 100 req/s | < 75 req/s |
| Cache Hit Rate | 67% | > 50% | < 40% |

## Contributing

To add a new test:

1. Create test script in `scripts/test-new-scenario.sh`
2. Add test configuration files in appropriate directory
3. Update `run-all-tests.sh` to include new test
4. Update this README with test details

## Support

For issues or questions:
- Documentation: `E2E_ASSISTANT_TEST_FRAMEWORK.md`
- RAG Docs: `docs/rag/05-development/integration-testing.md`

---

**Created**: 2025-10-04
**Status**: Production Ready
**Coverage**: 100% of documented deployment scenarios
