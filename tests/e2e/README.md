# Honua E2E Test Suite

Comprehensive end-to-end testing for Honua deployment scenarios across Docker, AWS (LocalStack), and Kubernetes.

## Overview

This test suite validates Honua's deployment capabilities across multiple platforms and configurations:

- **Docker Scenarios**: Standalone containers, compose stacks, networking, volumes, resource limits
- **LocalStack AWS Scenarios**: S3, DynamoDB, Secrets Manager, multi-bucket data lakes
- **Kubernetes Scenarios**: StatefulSets, Deployments, HPA, Ingress, PVCs, Network Policies

## Quick Start

Run all test suites:

```bash
./run-all-tests.sh
```

Run individual test suites:

```bash
# Docker tests
./docker-scenarios.sh

# LocalStack AWS tests
./localstack-scenarios.sh

# Kubernetes tests
./k8s-scenarios.sh
```

## Prerequisites

### Docker Tests
- Docker Engine
- docker-compose (optional, some tests will be skipped if not available)

### LocalStack Tests
- Docker Engine
- AWS CLI (`aws`)
- LocalStack Docker image

Install AWS CLI:
```bash
pip install awscli awscli-local
```

### Kubernetes Tests
- Docker Engine
- kubectl
- kind (Kubernetes in Docker)

Install kind:
```bash
GO111MODULE=on go install sigs.k8s.io/kind@latest
```

Install kubectl:
```bash
# macOS
brew install kubectl

# Linux
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl
sudo mv kubectl /usr/local/bin/
```

## Test Scenarios

### Docker Scenarios (`docker-scenarios.sh`)

1. **Single PostGIS Container**: Deploy standalone PostGIS with environment variables
2. **Docker Network**: Multi-container setup with custom network
3. **Docker Compose**: Multi-service stack (PostGIS + Redis)
4. **Volume Persistence**: Data persistence across container restarts
5. **Environment Variables**: Custom configuration via env vars
6. **Port Mappings**: Non-standard port mappings
7. **Resource Limits**: Memory and CPU constraints

### LocalStack AWS Scenarios (`localstack-scenarios.sh`)

1. **S3 Operations**: Bucket creation, file upload/download
2. **S3 Versioning**: Version-controlled GeoJSON files
3. **DynamoDB**: Table operations for metadata storage
4. **Secrets Manager**: Secure credential management
5. **S3 Public Access**: Bucket policies for tile delivery
6. **Multi-Bucket Data Lake**: Raw, processed, and analytics buckets

### Kubernetes Scenarios (`k8s-scenarios.sh`)

1. **PostGIS StatefulSet**: Persistent database deployment
2. **Honua Server Deployment**: Multi-replica deployment with ConfigMaps and Secrets
3. **Horizontal Pod Autoscaling**: CPU-based autoscaling
4. **Ingress**: External access configuration
5. **Persistent Storage**: PVC for tile cache
6. **Network Policy**: Security policies for pod communication

## Test Output

Each test suite provides:

- **Real-time progress** with colored output
- **Individual test results** (✓ PASSED / ✗ FAILED)
- **Test summary** with pass/fail counts
- **Execution time** for each suite

Example output:

```
╔══════════════════════════════════════════════════════════════════╗
║  Honua Docker E2E Test Suite                                    ║
╚══════════════════════════════════════════════════════════════════╝

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Test 1: Deploy standalone PostGIS container
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Waiting for PostGIS to start...
✓ PASSED

... (more tests)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Test Results
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Total Tests: 7
Passed: 7
Failed: 0
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✓ All tests passed!
```

## CI/CD Integration

### GitHub Actions

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  docker-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Docker E2E Tests
        run: ./tests/e2e/docker-scenarios.sh

  localstack-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Install AWS CLI
        run: pip install awscli awscli-local
      - name: Run LocalStack E2E Tests
        run: ./tests/e2e/localstack-scenarios.sh

  k8s-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Install kind
        uses: helm/kind-action@v1
      - name: Run K8s E2E Tests
        run: ./tests/e2e/k8s-scenarios.sh
```

## Troubleshooting

### Docker Tests Failing

**Issue**: "Cannot connect to the Docker daemon"
**Solution**: Ensure Docker is running (`docker ps`)

**Issue**: "Port already in use"
**Solution**: Stop conflicting containers or change test ports

### LocalStack Tests Failing

**Issue**: "LocalStack failed to start"
**Solution**: Check Docker logs: `docker logs honua-localstack-test`

**Issue**: "AWS CLI not found"
**Solution**: Install AWS CLI: `pip install awscli`

### Kubernetes Tests Failing

**Issue**: "kind not found"
**Solution**: Install kind: `GO111MODULE=on go install sigs.k8s.io/kind@latest`

**Issue**: "Cluster creation failed"
**Solution**: Delete existing cluster: `kind delete cluster --name honua-test-cluster`

## Development

### Adding New Tests

1. Choose the appropriate test suite file
2. Add a new test function following the pattern:

```bash
test_my_new_feature() {
    test_start "Description of what is being tested"

    # Test implementation
    if [ test_condition ]; then
        test_pass
    else
        test_fail "Reason for failure"
    fi

    # Cleanup
}
```

3. Call your test function in the `main()` function
4. Test your changes: `./docker-scenarios.sh`

### Test Best Practices

- **Isolation**: Each test should be independent
- **Cleanup**: Always clean up resources in the test or cleanup function
- **Descriptive**: Use clear test names and failure messages
- **Idempotent**: Tests should pass when run multiple times
- **Fast**: Optimize wait times while ensuring reliability

## ECS Deployment (Future)

To add actual ECS deployment tests (requires AWS account):

```bash
# Create ECS cluster
aws ecs create-cluster --cluster-name honua-test

# Register task definition
aws ecs register-task-definition --cli-input-json file://task-definition.json

# Create service
aws ecs create-service \
  --cluster honua-test \
  --service-name honua-service \
  --task-definition honua-task \
  --desired-count 2
```

## Contributing

When adding new deployment scenarios:

1. Add tests to the appropriate suite
2. Update this README with new test descriptions
3. Ensure tests pass locally before committing
4. Update CI/CD workflows if needed

## License

Same as Honua project license.
