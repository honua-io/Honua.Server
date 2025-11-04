# Tests for Azure Container Apps Module

This directory contains automated tests for the Honua Azure Container Apps Terraform module.

## Test Structure

```
tests/
├── unit/           # Unit tests with minimal configuration
├── integration/    # Integration tests with full configuration
└── README.md       # This file
```

## Running Tests

### Prerequisites

- Terraform >= 1.5.0
- Azure credentials configured
- Azure CLI installed (optional, for authentication)

### Unit Tests

Unit tests validate Terraform syntax, variable types, and basic module structure without creating real infrastructure.

```bash
cd tests/unit
terraform init -backend=false
terraform validate
terraform plan
```

**What Unit Tests Check:**
- Terraform syntax is valid
- Variables have proper types and validation
- Outputs are correctly defined
- Required providers are specified
- Module compiles without errors
- Minimal resource configuration works

### Integration Tests

Integration tests validate the full module with all features enabled. These tests use `terraform plan` to validate configuration but don't actually create resources.

```bash
cd tests/integration
terraform init -backend=false
terraform validate
terraform plan
```

**What Integration Tests Check:**
- Full production-like configuration validates
- All resources are properly configured
- Dependencies are correctly ordered
- Outputs match expected values
- Complex configurations work together

### Testing with Real Resources (Optional)

To test with actual Azure resources (will incur costs):

```bash
cd tests/integration

# Ensure Azure credentials are configured
az login
az account show

# Set container image
export TF_VAR_container_image="myregistry.azurecr.io/honua:latest"

# Initialize with a backend
terraform init

# Plan the deployment
terraform plan -out=tfplan

# Review the plan carefully!
terraform show tfplan

# Apply (WARNING: Creates real resources and incurs costs)
terraform apply tfplan

# Don't forget to destroy afterwards
terraform destroy
```

## Test Coverage

- [x] Terraform syntax validation
- [x] Variable type checking and validation rules
- [x] Output generation
- [x] Required provider configuration
- [x] Container Apps configuration
- [x] Container Apps Environment
- [x] Azure Database for PostgreSQL (Flexible Server)
- [x] Virtual Network and subnets
- [x] Private DNS zones
- [x] Key Vault for secrets
- [x] Managed Identity
- [x] Azure Front Door (CDN + Global LB)
- [x] Log Analytics workspace
- [x] Diagnostic settings
- [x] Role assignments
- [x] Environment variable handling
- [x] Scaling configuration
- [x] Health check configuration
- [x] Tags

## CI/CD Integration

Tests run automatically on:
- Pull requests modifying Terraform code in this module
- Commits to `main` or `dev` branches

See `.github/workflows/terraform-test.yml` for CI/CD configuration.

## Test Configuration

### Unit Test Configuration

The unit test uses minimal settings:
- No database (PostgreSQL disabled)
- No Key Vault
- No Front Door
- Uses existing resource group and VNet
- Minimal scaling (0-1 replicas)
- Minimal resources (0.25 CPU, 0.5Gi memory)

### Integration Test Configuration

The integration test uses full production-like settings:
- Azure Database for PostgreSQL Flexible Server
- Azure Front Door (Standard SKU)
- Virtual Network with delegated subnets
- Key Vault for secrets
- Managed Identity with role assignments
- Log Analytics workspace
- Horizontal scaling (0-10 replicas)
- Comprehensive monitoring and diagnostics
- Full resource creation

## Validation Outputs

Both tests output validation results showing whether:
- Resources were properly configured
- Outputs are generated correctly
- Dependencies are satisfied
- Configuration matches expected values

Example validation output:

```hcl
test_validation = {
  environment_valid     = true
  min_replicas_valid    = true
  max_replicas_valid    = true
  database_disabled     = true
  app_url_generated     = true
}
```

## Security Testing

Run security scans with `tfsec`:

```bash
# Install tfsec
brew install tfsec

# Scan the module
tfsec ../..

# Scan with specific checks
tfsec ../.. --minimum-severity HIGH
```

## Cost Estimation

Estimate costs with Infracost:

```bash
# Install infracost
brew install infracost

# Generate cost estimate
cd tests/integration
infracost breakdown --path .
```

## Troubleshooting

### Backend Errors

If you see backend configuration errors during testing:
```
Error: Backend initialization required
```

Solution: Use `-backend=false` flag:
```bash
terraform init -backend=false
```

### Azure Authentication

If you see authentication errors:
```
Error: Unable to authenticate with Azure
```

Solution: Authenticate with Azure CLI:
```bash
az login
az account set --subscription "your-subscription-id"
```

### Resource Provider Registration

If you see provider registration errors:
```
Error: The subscription is not registered to use namespace 'Microsoft.App'
```

Solution: Register required providers:
```bash
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
az provider register --namespace Microsoft.DBforPostgreSQL
```

For testing without actual Azure resources, set:
```bash
export TF_VAR_skip_azure_validation=true
```

### Variable Validation Errors

If variables fail validation:
```
Error: Invalid value for variable
```

Solution: Check that test variables match validation rules in `../../variables.tf`

## Cleanup

To remove all test resources (if you ran `terraform apply`):

```bash
cd tests/integration

# Destroy all resources
terraform destroy -auto-approve

# Verify cleanup
az group list --query "[?contains(name, 'honua-int-test')]"
```

## Contributing

When adding new features to the module:

1. Update unit tests to cover basic functionality
2. Update integration tests to cover full configuration
3. Add validation outputs to verify correct behavior
4. Update this README with any new test requirements
5. Run tests locally before submitting PR

## Best Practices

1. **Always run tests locally** before pushing
2. **Use `-backend=false`** for validation-only tests
3. **Never commit sensitive data** (credentials, keys, etc.)
4. **Tag test resources** with `ephemeral = "true"` for easy cleanup
5. **Review plan output** carefully before applying
6. **Destroy test resources** promptly to avoid costs
7. **Use variable validation** to catch errors early
8. **Keep tests fast** - unit tests should complete in < 30 seconds

## Known Limitations

- Container Apps require a delegated subnet
- PostgreSQL Flexible Server requires a private DNS zone
- Front Door custom domains require DNS zone configuration
- Some features may not be available in all Azure regions
- Key Vault soft delete requires purge protection in production

## Support

For issues or questions about testing:
- Check existing GitHub issues
- Review Terraform documentation: https://developer.hashicorp.com/terraform
- Review Azure Container Apps documentation: https://learn.microsoft.com/azure/container-apps/
