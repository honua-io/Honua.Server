# Tests for AWS Lambda + ALB Module

This directory contains automated tests for the Honua Lambda Terraform module.

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
- AWS credentials configured (or use validation-only mode)
- AWS CLI installed (optional, for authentication)

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

To test with actual AWS resources (will incur costs):

```bash
cd tests/integration

# Ensure AWS credentials are configured
aws sts get-caller-identity

# Set skip validation to false
export TF_VAR_skip_aws_validation=false

# Set container image URI
export TF_VAR_container_image_uri="YOUR_ECR_REPO_URI:latest"

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
- [x] Lambda function configuration
- [x] Lambda Function URL
- [x] Application Load Balancer (ALB)
- [x] VPC and networking configuration
- [x] RDS PostgreSQL database
- [x] Secrets Manager integration
- [x] ECR repository
- [x] IAM roles and policies
- [x] Security groups
- [x] CloudWatch Logs
- [x] NAT Gateway
- [x] Lambda autoscaling
- [x] Environment variable handling
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
- No database (RDS disabled)
- No ALB
- No VPC creation (uses existing)
- Function URL enabled for simple access
- Minimal Lambda resources (512 MB memory)
- No autoscaling

### Integration Test Configuration

The integration test uses full production-like settings:
- RDS PostgreSQL database
- Application Load Balancer
- Full VPC with public/private subnets
- NAT Gateway for private subnet access
- Lambda autoscaling (0-10 provisioned concurrency)
- ECR repository
- Performance Insights enabled
- Comprehensive monitoring and logging
- Function URL + ALB for dual access patterns

## Validation Outputs

Both tests output validation results showing whether:
- Resources were properly configured
- Outputs are generated correctly
- Dependencies are satisfied
- Configuration matches expected values

Example validation output:

```hcl
test_validation = {
  environment_valid       = true
  database_disabled       = true
  function_url_created    = true
  lambda_function_created = true
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

### AWS Authentication

If you see authentication errors:
```
Error: error configuring Terraform AWS Provider
```

Solution: Configure AWS credentials:
```bash
aws configure
# Or use environment variables
export AWS_ACCESS_KEY_ID="your-key"
export AWS_SECRET_ACCESS_KEY="your-secret"
```

For validation-only testing, set:
```bash
export TF_VAR_skip_aws_validation=true
```

### ECR Image URI

If you need to test with real resources, push an image to ECR first:

```bash
# Create ECR repository
aws ecr create-repository --repository-name honua-test

# Build and push image
docker build -t honua:test .
aws ecr get-login-password | docker login --username AWS --password-stdin YOUR_ACCOUNT.dkr.ecr.us-east-1.amazonaws.com
docker tag honua:test YOUR_ACCOUNT.dkr.ecr.us-east-1.amazonaws.com/honua-test:latest
docker push YOUR_ACCOUNT.dkr.ecr.us-east-1.amazonaws.com/honua-test:latest
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
aws lambda list-functions --query 'Functions[?contains(FunctionName, `honua-int-test`)]'
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

- Unit tests use mock AWS endpoints and won't fully validate AWS-specific constraints
- Integration tests require valid AWS credentials to fully test
- Some features (like Performance Insights) may not be available in all regions
- NAT Gateway can be expensive; consider using VPC endpoints in production

## Support

For issues or questions about testing:
- Check existing GitHub issues
- Review Terraform documentation: https://developer.hashicorp.com/terraform
- Review AWS Lambda documentation: https://docs.aws.amazon.com/lambda/
