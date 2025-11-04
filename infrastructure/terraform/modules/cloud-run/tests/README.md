# Tests for Google Cloud Run Module

This directory contains automated tests for the Honua Cloud Run Terraform module.

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
- GCP credentials configured
- `gcloud` CLI installed (optional, for authentication)

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

To test with actual GCP resources (will incur costs):

```bash
cd tests/integration

# Set your project ID
export TF_VAR_project_id="your-gcp-project"
export TF_VAR_vpc_network_id="projects/your-gcp-project/global/networks/default"

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
- [x] Cloud Run service configuration
- [x] Database (Cloud SQL) configuration
- [x] VPC connector configuration
- [x] Load balancer with SSL configuration
- [x] CDN configuration
- [x] Cloud Armor security policy
- [x] IAM and service account configuration
- [x] Secret Manager integration
- [x] Environment variable handling
- [x] Scaling configuration
- [x] Health check configuration
- [x] Labels and tagging

## CI/CD Integration

Tests run automatically on:
- Pull requests modifying Terraform code in this module
- Commits to `main` or `dev` branches

See `.github/workflows/terraform-test.yml` for CI/CD configuration.

## Test Configuration

### Unit Test Configuration

The unit test uses minimal settings:
- No database (Cloud SQL disabled)
- No load balancer
- No VPC connector
- Minimal scaling (0-1 instances)
- Public access enabled
- Cloud Armor disabled

### Integration Test Configuration

The integration test uses full production-like settings:
- Cloud SQL PostgreSQL with PostGIS
- Global load balancer with SSL
- VPC connector for private networking
- Horizontal scaling (0-5 instances)
- CDN enabled
- Cloud Armor security policy
- Comprehensive monitoring

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
  min_instances_valid   = true
  max_instances_valid   = true
  database_disabled     = true
  service_url_generated = true
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

### Provider Authentication

If you see authentication errors:
```
Error: google: could not find default credentials
```

Solution: Authenticate with gcloud:
```bash
gcloud auth application-default login
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
terraform destroy -auto-approve
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

## Support

For issues or questions about testing:
- Check existing GitHub issues
- Review Terraform documentation: https://developer.hashicorp.com/terraform
- Review GCP Cloud Run documentation: https://cloud.google.com/run/docs
