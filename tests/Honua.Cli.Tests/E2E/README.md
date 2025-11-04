# Full End-to-End Deployment Tests

This directory contains comprehensive end-to-end tests that validate the complete workflow from natural language requirements to deployed HonuaIO infrastructure with validated service endpoints.

## MultiCloudDeploymentE2ETest (SKIPPED BY DEFAULT)

**Status**: These tests are **skipped by default** in CI/CD pipelines due to long execution times (1+ hour) and infrastructure requirements.

**What it tests:**
- Multi-cloud Terraform generation (AWS, Azure, GCP)
- Kubernetes manifest generation
- Docker Compose deployment
- Infrastructure validation with emulators (LocalStack, Azurite)

**Why skipped:**
- Each test makes real LLM API calls (costs money)
- Requires infrastructure tools (terraform, docker, kubectl)
- Full suite takes 60-90 minutes to complete
- Tests infrastructure generation, not service deployment

**Prerequisites:**
- LLM API keys (OpenAI or Anthropic)
- Terraform v1.0+
- Docker and Docker Compose
- kubectl (for Kubernetes tests)
- Optional: minikube (for K8s deployment validation)

**To enable these tests:**
Remove the `Skip` attribute from the test methods in `MultiCloudDeploymentE2ETest.cs` and run:
```bash
dotnet test tests/Honua.Cli.Tests --filter "Category=E2E" --logger "console;verbosity=detailed"
```

**Test Coverage:**
1. AWS_TerraformGeneration_WithLocalStack_ShouldGenerateAndValidate
2. Azure_ResourceGeneration_WithAzurite_ShouldGenerateConfiguration
3. GCP_TerraformGeneration_ShouldGenerateAndValidate
4. Kubernetes_ManifestGeneration_ShouldCreateValidYAML
5. DockerCompose_WithPostGIS_ShouldDeployAndValidate

---

## FullDeploymentE2ETest

**What it does:**
1. Uses **real LLM** (OpenAI or Claude) to analyze deployment requirements
2. Generates infrastructure configuration (Docker Compose or Terraform)
3. Executes the deployment (runs docker compose up or terraform apply)
4. **Validates service endpoints** (checks Honua server health, PostgreSQL/PostGIS)
5. Tears down infrastructure cleanly

**Prerequisites:**
- Docker and Docker Compose installed
- OpenAI or Anthropic API key configured in user secrets
- (Optional) Cloud credentials for Terraform-based tests

## Running the Tests

### Option 1: Run with user secrets (Recommended)

The test automatically reads API keys from ASP.NET user secrets:

```bash
# Store your API keys (one-time setup)
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-..." --project tests/Honua.Cli.Tests/Honua.Cli.Tests.csproj
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project tests/Honua.Cli.Tests/Honua.Cli.Tests.csproj

# Run the full deployment test
dotnet test tests/Honua.Cli.Tests/Honua.Cli.Tests.csproj \
  --filter "FullyQualifiedName~FullDeploymentE2ETest" \
  --logger "console;verbosity=detailed"
```

### Option 2: Run with environment variables

```bash
export OPENAI_API_KEY="sk-proj-..."
# OR
export ANTHROPIC_API_KEY="sk-ant-..."

dotnet test tests/Honua.Cli.Tests/Honua.Cli.Tests.csproj \
  --filter "FullyQualifiedName~FullDeploymentE2ETest" \
  --logger "console;verbosity=detailed"
```

### Option 3: Manual deployment test (no automated test runner)

```bash
# Navigate to test workspace
cd /tmp/honua-manual-test

# Run consultant with real LLM
export OPENAI_API_KEY="sk-proj-..."
dotnet run --project /path/to/Honua.Cli -- consultant \
  --prompt "Deploy HonuaIO to Docker Compose with PostgreSQL PostGIS" \
  --workspace . \
  --no-interactive \
  --auto-approve

# Verify deployment
docker compose ps
curl http://localhost:8080/health

# Validate PostgreSQL
docker exec <container-name> psql -U postgres -d honua -c "SELECT PostGIS_version();"

# Cleanup
docker compose down -v
```

## Test Workflow Details

### 1. LLM Analysis Phase
- Sends natural language requirements to OpenAI/Claude
- LLM analyzes workspace context (existing files, infrastructure)
- Generates a deployment plan with specific steps

### 2. Configuration Generation Phase
- Creates `docker-compose.yml` or Terraform `.tf` files
- Configures PostgreSQL with PostGIS extension
- Sets up Honua server with environment variables

### 3. Execution Phase
- Runs `docker compose up -d` or `terraform init && terraform apply`
- Waits for services to be healthy (health checks)
- Monitors deployment progress

### 4. Validation Phase
- **Health check**: `GET http://localhost:8080/health`
- **PostGIS validation**: Runs SQL query to verify PostGIS extension
- **Metadata validation**: Checks if metadata catalog is accessible
- **Service connectivity**: Verifies Honua server can connect to PostgreSQL

### 5. Teardown Phase
- Stops all containers: `docker compose down -v`
- Removes test workspace
- Cleans up temporary files

## Expected Output

```
=== Step 1: Generate deployment plan with real LLM ===
Using LLM: OpenAI
Plan generated: ...

=== Step 2: Execute deployment ===
[+] Running 2/2
 ⠿ Container honua-postgis-1       Healthy
 ⠿ Container honua-server-1        Started
Deployment successful

=== Step 3: Wait for services ===
Waiting 15 seconds for services to initialize...

=== Step 4: Validate service endpoints ===
Validating Honua server endpoint...
✓ Honua server is healthy: {"status":"healthy","version":"1.0.0"}

Validating PostgreSQL/PostGIS...
✓ PostGIS is working:
 postgis_version
-----------------
 3.3 USE_GEOS=1...

=== ALL VALIDATIONS PASSED ===
```

## Troubleshooting

### Test skipped - no API keys
```
SKIPPED: No real LLM API keys available
```
**Solution**: Configure API keys in user secrets (see Option 1 above)

### Docker not running
```
Error: Cannot connect to the Docker daemon
```
**Solution**: Start Docker Desktop or dockerd service

### Port already in use
```
Error: Bind for 0.0.0.0:8080 failed: port is already allocated
```
**Solution**: Stop other services using ports 8080 or 5432

### Health check timeout
```
Honua server health check failed after 10 attempts
```
**Solution**: Check container logs: `docker logs honua-server-1`

## Configuration Options

The test is currently configured for Docker Compose deployment. To test Terraform:

1. Modify the consultant prompt in the test
2. Ensure cloud credentials are configured (AWS CLI, Azure CLI, or gcloud)
3. Update validation logic to use cloud endpoints instead of localhost

## Cost Warning

⚠️ **The Terraform-based tests will create real cloud resources and incur costs!**

- Docker Compose tests are free (runs locally)
- Terraform tests create actual VMs, databases, load balancers
- Estimated cost for AWS test: $0.10-$0.50 per test run
- Always verify cleanup completed: `terraform destroy`

## Next Steps

After this test passes, you have validated:
- ✅ Real LLM can understand deployment requirements
- ✅ Infrastructure code is generated correctly
- ✅ Deployment executes successfully
- ✅ Services are reachable and functional
- ✅ PostgreSQL/PostGIS is configured properly

You can now confidently use the consultant for production deployments!
