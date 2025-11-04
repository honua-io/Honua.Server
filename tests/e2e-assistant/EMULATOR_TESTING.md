# AI Consultant E2E Testing with Cloud Emulators

## Overview

The AI consultant tests can run in two modes:

1. **Configuration Validation** - AI generates configs, tests validate correctness (fast)
2. **Emulator Deployment** - AI generates configs + deploys to LocalStack/Azurite (comprehensive)

## Quick Start

### Configuration Validation Only (Fast)

```bash
# Set API keys
export ANTHROPIC_API_KEY=sk-ant-your-key-here
# OR
export OPENAI_API_KEY=sk-your-key-here

# Run all platform tests (validates AI-generated configs)
./run-all-ai-consultant-platform-tests.sh
```

### Full Deployment with Emulators (Comprehensive)

```bash
# Start emulators and run tests
./run-ai-consultant-with-emulators.sh
```

## What Was Fixed

### 1. Removed Dry-Run Mode
- **Before**: Tests ran with `--dry-run` flag, skipping terraform execution
- **After**: Tests attempt full deployment (will fail without emulators/credentials)
- **Reason**: You have emulators available (LocalStack, Azurite) and want real deployments

### 2. Fixed Kubernetes Manifest Generation Bug
- **Issue**: DeploymentConfiguration agent tried to write kubernetes directory as a file
- **Fix**: Skip SaveConfigurationAsync for kubernetes-manifests (files already written)
- **Result**: Kubernetes tests now generate manifests successfully

### 3. Added Emulator Test Script
- **Script**: `run-ai-consultant-with-emulators.sh`
- **Features**:
  - Starts LocalStack (AWS) and Azurite (Azure)
  - Configures environment variables for emulators
  - Runs AI consultant to generate terraform
  - Executes terraform init/plan against emulators
  - Shows detailed logs of what's happening

## Test Structure

### AI Consultant Platform Tests (Validation Only)

These tests validate that the AI generates correct configuration files:

```
tests/e2e-assistant/scripts/
├── test-ai-consultant-interactive-requirements.sh  # Requirements gathering
├── test-ai-consultant-docker-comprehensive.sh      # Docker configs
├── test-ai-consultant-aws-ecs.sh                   # AWS ECS configs
├── test-ai-consultant-azure-container-apps.sh      # Azure configs
├── test-ai-consultant-gcp-cloud-run.sh             # GCP configs
└── test-ai-consultant-kubernetes.sh                # K8s manifests
```

**What they test**:
- ✅ AI generates syntactically correct configs
- ✅ Configs contain required resources
- ✅ AI adapts to different requirements
- ❌ Does NOT deploy infrastructure

### Emulator Tests (Full Deployment)

```bash
./run-ai-consultant-with-emulators.sh
```

**What they test**:
- ✅ AI generates configs
- ✅ Configs are valid
- ✅ Terraform init succeeds
- ✅ Terraform plan succeeds (against emulators)
- ⚠️ Terraform apply (optional, emulators have limitations)

## Cloud Emulators

### LocalStack (AWS)

**Services Emulated**:
- S3 (object storage)
- ECS (container orchestration)
- RDS (relational database)
- Secrets Manager
- CloudWatch
- ElastiCache
- IAM

**Endpoint**: `http://localhost:4566`

**Credentials**:
```bash
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1
export AWS_ENDPOINT_URL=http://localhost:4566
```

### Azurite (Azure)

**Services Emulated**:
- Blob Storage
- Queue Storage
- Table Storage

**Endpoints**:
- Blob: `http://localhost:10000`
- Queue: `http://localhost:10001`
- Table: `http://localhost:10002`

**Connection String**:
```bash
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
```

### GCP (Future)

For GCP emulation, consider:
- **fake-gcs-server**: S3-compatible GCS emulator
- **Cloud SQL Proxy**: Local PostgreSQL with GCP-like config

## Viewing Test Output

### Real-Time Output

All test scripts now show real-time output:

```bash
./run-all-ai-consultant-platform-tests.sh
```

You'll see:
- AI consultant reasoning
- Configuration generation progress
- Validation results
- Terraform execution (if not in dry-run)

### Test Logs

Detailed logs are saved in:
```
tests/e2e-assistant/results/
├── platform-tests_TIMESTAMP/
│   ├── Requirements_Gathering.log
│   ├── Docker_Comprehensive.log
│   ├── AWS_ECS.log
│   └── SUMMARY.md
└── emulator-tests_TIMESTAMP/
    ├── aws-ecs-localstack/
    │   ├── consultant.log
    │   ├── terraform-init.log
    │   └── terraform-plan.log
    └── azure-container-apps/
```

### Consultant Session Logs

Full AI consultant transcripts:
```
~/.config/Honua/logs/
├── consultant-YYYYMMDD.md              # Human-readable session log
└── consultant-YYYYMMDD-multi-*.json    # Machine-readable multi-agent transcript
```

## Expected Failures

### LocalStack Limitations

LocalStack free tier has limitations:
- **ECS**: Task definitions work, but task execution is limited
- **RDS**: Database creation works, but some advanced features missing
- **CloudWatch**: Basic metrics only

**Expected behavior**:
- ✅ Terraform init succeeds
- ✅ Terraform plan succeeds
- ⚠️ Terraform apply may fail on some resources

### Configuration Validation vs Deployment

**Configuration tests** (fast):
- Generate configs
- Validate syntax
- Check for required resources
- **Exit code 0** even if not deployed

**Emulator tests** (comprehensive):
- Generate configs
- Deploy to emulators
- Validate infrastructure
- **Exit code 0** only if deployment succeeds

## Troubleshooting

### "Multi-agent coordination did not complete successfully"

This error means one of the agents failed:

1. Check the consultant log:
   ```bash
   cat results/latest-test/consultant.log
   ```

2. Check the multi-agent transcript:
   ```bash
   cat ~/.config/Honua/logs/consultant-*-multi-*.json | jq '.steps[] | select(.success == false)'
   ```

3. Common causes:
   - Kubernetes directory permission errors (FIXED)
   - Terraform provider not found
   - Missing cloud credentials (expected without emulators)

### "Access denied" Errors

Fixed in commit `XXXX`:
- Kubernetes manifests now write correctly
- SaveConfigurationAsync skips kubernetes-manifests type

### Emulator Not Starting

```bash
# Check if LocalStack is running
docker ps | grep localstack

# Check LocalStack health
curl http://localhost:4566/_localstack/health

# Restart LocalStack
docker restart honua-localstack-e2e
```

## Next Steps

### Add More Emulator Tests

Extend `run-ai-consultant-with-emulators.sh` to test:
- Azure Container Apps with Azurite
- GCP Cloud Run with fake-gcs-server
- Kubernetes with minikube/kind

### Integration with CI/CD

```yaml
# .github/workflows/ai-e2e-tests.yml
name: AI Consultant E2E Tests

on: [push, pull_request]

jobs:
  emulator-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start LocalStack
        run: docker-compose -f tests/e2e-assistant/docker-compose-emulators.yml up -d
      - name: Run AI Tests
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: ./tests/e2e-assistant/run-ai-consultant-with-emulators.sh
```

### Terraform Apply Support

To enable full `terraform apply`:
1. Add `--auto-apply` flag to consultant command
2. Configure DeploymentExecution agent to run apply
3. Add cleanup step to destroy resources after test

## Summary

✅ **What's Working**:
- AI generates correct configurations
- Tests show real-time output
- Kubernetes manifest generation fixed
- Emulator framework created

⚠️ **What's Not Yet Implemented**:
- Full terraform apply in emulators
- GCP emulator integration
- Automatic cleanup of emulator resources

🎯 **Recommended Usage**:
- Use `run-all-ai-consultant-platform-tests.sh` for fast CI/CD validation
- Use `run-ai-consultant-with-emulators.sh` for comprehensive pre-release testing
- Use existing `test-localstack-*.sh` for infrastructure-specific testing
