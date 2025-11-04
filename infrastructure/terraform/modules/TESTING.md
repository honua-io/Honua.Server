# Terraform Module Testing Guide

Comprehensive testing documentation for Honua serverless Terraform modules.

## Table of Contents

1. [Overview](#overview)
2. [Quick Start](#quick-start)
3. [Test Structure](#test-structure)
4. [Running Tests](#running-tests)
5. [Module-Specific Tests](#module-specific-tests)
6. [CI/CD Integration](#cicd-integration)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

## Overview

This directory contains Terraform modules for deploying Honua GIS Platform on various serverless platforms. Each module includes comprehensive tests to ensure reliability and correctness.

### Tested Modules

- **cloud-run** - Google Cloud Run deployment
- **lambda** - AWS Lambda + ALB deployment
- **container-apps** - Azure Container Apps deployment
- **cdn** - Cloud-agnostic CDN (CloudFront, Cloud CDN, Front Door)

### Test Types

Each module includes two types of tests:

1. **Unit Tests** - Minimal configuration, syntax validation only
2. **Integration Tests** - Full production-like configuration

## Quick Start

### Run All Tests

```bash
cd infrastructure/terraform/modules
./test-all.sh
```

### Run Tests for Specific Module

```bash
cd infrastructure/terraform/modules/cloud-run/tests/unit
terraform init -backend=false
terraform validate
terraform plan
```

### Run in CI/CD

Tests run automatically on pull requests and commits to main/dev branches via GitHub Actions.

## Test Structure

```
modules/
├── cloud-run/
│   ├── tests/
│   │   ├── unit/
│   │   │   ├── main.tf
│   │   │   └── variables.tf
│   │   ├── integration/
│   │   │   ├── main.tf
│   │   │   └── variables.tf
│   │   └── README.md
│   ├── main.tf
│   ├── variables.tf
│   └── outputs.tf
├── lambda/
│   └── tests/...
├── container-apps/
│   └── tests/...
├── cdn/
│   └── tests/...
├── test-all.sh
└── TESTING.md (this file)
```

## Running Tests

### Prerequisites

Install required tools:

```bash
# Terraform
brew install terraform

# Cloud provider CLIs (optional)
brew install google-cloud-sdk  # GCP
brew install awscli            # AWS
brew install azure-cli         # Azure

# Testing tools (optional)
brew install tfsec      # Security scanning
brew install infracost  # Cost estimation
```

### Local Testing

#### Validation-Only Testing (No Credentials Required)

Run tests without cloud provider credentials for syntax validation:

```bash
cd infrastructure/terraform/modules

# Run all tests
./test-all.sh

# Or test individual modules
cd cloud-run/tests/unit
terraform init -backend=false
terraform validate
terraform plan
```

#### Full Testing (With Cloud Credentials)

Test with actual cloud provider validation (no resources created):

```bash
# Google Cloud
gcloud auth application-default login
cd cloud-run/tests/integration
terraform init -backend=false
terraform validate
terraform plan

# AWS
aws configure
cd lambda/tests/integration
export TF_VAR_skip_aws_validation=false
terraform init -backend=false
terraform validate
terraform plan

# Azure
az login
cd container-apps/tests/integration
terraform init -backend=false
terraform validate
terraform plan
```

#### Real Resource Testing (Incurs Costs!)

**⚠️ WARNING: This creates actual cloud resources and incurs costs!**

```bash
cd cloud-run/tests/integration

# Set required variables
export TF_VAR_project_id="your-gcp-project"
export TF_VAR_container_image="gcr.io/your-project/honua:latest"

# Initialize with backend
terraform init

# Review plan carefully
terraform plan -out=tfplan
terraform show tfplan

# Apply (creates real resources)
terraform apply tfplan

# IMPORTANT: Destroy afterwards
terraform destroy
```

### CI/CD Testing

Tests run automatically on GitHub Actions:

```yaml
# Triggers
- Pull requests modifying Terraform code
- Commits to main/master/dev branches
- Manual workflow dispatch

# Jobs
- Terraform validate
- Terraform plan
- Format check
- Security scan (tfsec)
- Cost estimation (Infracost)
```

View workflow: `.github/workflows/terraform-test.yml`

## Module-Specific Tests

### Cloud Run Module

Tests Google Cloud Run deployment with Cloud SQL, VPC, Load Balancer, and CDN.

```bash
cd cloud-run/tests

# Unit test - minimal config
cd unit
terraform init -backend=false
terraform validate
terraform plan

# Integration test - full config
cd ../integration
terraform init -backend=false
terraform validate
terraform plan
```

**What's Tested:**
- Cloud Run service configuration
- Cloud SQL PostgreSQL with PostGIS
- VPC connector for private networking
- Global Load Balancer with SSL
- Cloud CDN for caching
- Cloud Armor security policy
- Secret Manager integration
- IAM and service accounts

### Lambda Module

Tests AWS Lambda deployment with RDS, VPC, and ALB.

```bash
cd lambda/tests

# Unit test
cd unit
terraform init -backend=false
terraform validate
terraform plan

# Integration test
cd ../integration
export TF_VAR_skip_aws_validation=false  # For full validation
terraform init -backend=false
terraform validate
terraform plan
```

**What's Tested:**
- Lambda function with container image
- Lambda Function URL
- Application Load Balancer
- RDS PostgreSQL database
- VPC with public/private subnets
- NAT Gateway
- ECR repository
- Secrets Manager
- CloudWatch Logs
- Lambda autoscaling

### Container Apps Module

Tests Azure Container Apps with PostgreSQL and Front Door.

```bash
cd container-apps/tests

# Unit test
cd unit
terraform init -backend=false
terraform validate
terraform plan

# Integration test
cd ../integration
terraform init -backend=false
terraform validate
terraform plan
```

**What's Tested:**
- Container Apps configuration
- Azure Database for PostgreSQL Flexible Server
- Virtual Network with delegated subnets
- Private DNS zones
- Key Vault for secrets
- Managed Identity
- Azure Front Door (CDN + Global LB)
- Log Analytics workspace
- Diagnostic settings

### CDN Module

Tests cloud-agnostic CDN configuration.

```bash
cd cdn/tests

# Unit test - AWS CloudFront
cd unit
terraform init -backend=false
terraform validate
terraform plan

# Integration test - multiple providers
cd ../integration
terraform init -backend=false
terraform validate
terraform plan
```

**What's Tested:**
- AWS CloudFront with Origin Shield
- GCP Cloud CDN configuration output
- Azure Front Door configuration
- GIS-optimized cache policies (tiles, features, metadata)
- Query string caching for GIS parameters
- SSL/TLS configuration
- Custom domain support

## CI/CD Integration

### GitHub Actions Workflow

The workflow automatically:

1. **Detects Changed Modules** - Only tests affected modules
2. **Runs Tests in Parallel** - Each module tested independently
3. **Validates Format** - Checks `terraform fmt`
4. **Scans Security** - Uses `tfsec` for security issues
5. **Estimates Costs** - Uses Infracost on PRs
6. **Uploads Artifacts** - Saves plan files for review

### Workflow Configuration

```yaml
# File: .github/workflows/terraform-test.yml

# Triggered by:
- Pull requests to main/master/dev
- Push to main/master/dev
- Manual workflow dispatch

# Jobs:
- detect-changes    # Detect which modules changed
- test-cloud-run    # Test Cloud Run module
- test-lambda       # Test Lambda module
- test-container-apps # Test Container Apps module
- test-cdn          # Test CDN module
- security-scan     # tfsec security scanning
- cost-estimation   # Infracost cost estimation (PRs only)
- test-summary      # Aggregate results
```

### Required Secrets

Add these to GitHub repository secrets (optional):

```bash
# For Infracost (optional)
INFRACOST_API_KEY=your_infracost_api_key
```

### Viewing Results

1. Go to GitHub repository → Actions tab
2. Select "Terraform Module Tests" workflow
3. View individual job logs
4. Download plan artifacts if needed

## Best Practices

### 1. Always Test Locally Before Pushing

```bash
cd infrastructure/terraform/modules
./test-all.sh
```

### 2. Use `-backend=false` for Tests

Never configure remote backends in test files:

```bash
terraform init -backend=false
```

### 3. Tag Test Resources

Always tag resources for easy cleanup:

```hcl
tags = {
  ephemeral    = "true"
  test_type    = "integration"
  auto_cleanup = "true"
}
```

### 4. Clean Up Test Resources

If you create real resources:

```bash
terraform destroy -auto-approve
```

### 5. Use Variable Validation

Add validation to catch errors early:

```hcl
variable "environment" {
  type = string
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}
```

### 6. Test Both Success and Failure Cases

Include tests for:
- Valid configurations
- Invalid configurations (should fail validation)
- Edge cases
- Default values

### 7. Keep Tests Fast

- Unit tests should complete in < 30 seconds
- Integration tests should complete in < 2 minutes
- Don't create real resources in tests

### 8. Document Test Requirements

Each module's tests/README.md should document:
- Prerequisites
- How to run tests
- What is tested
- Known limitations

## Troubleshooting

### Common Issues

#### Backend Configuration Errors

```
Error: Backend initialization required
```

**Solution:**
```bash
terraform init -backend=false
```

#### Provider Authentication

```
Error: could not find default credentials
```

**Solution:**
```bash
# GCP
gcloud auth application-default login

# AWS
aws configure

# Azure
az login

# Or use skip validation flags
export TF_VAR_skip_aws_validation=true
```

#### Format Check Failures

```
Error: terraform fmt -check failed
```

**Solution:**
```bash
terraform fmt -recursive
```

#### Variable Validation Errors

```
Error: Invalid value for variable
```

**Solution:** Check variable validation rules in `variables.tf`

#### Module Not Found

```
Error: Module not found
```

**Solution:** Ensure module path is correct and `source = "../.."` points to module root

### Debugging Tests

#### Enable Verbose Output

```bash
TF_LOG=DEBUG terraform plan
```

#### Check Generated Plan

```bash
terraform plan -out=tfplan
terraform show tfplan
```

#### Validate JSON

```bash
terraform show -json tfplan | jq .
```

### Getting Help

1. Check module-specific README in `tests/` directory
2. Review GitHub Actions logs for CI failures
3. Search existing GitHub issues
4. Create new issue with:
   - Module name
   - Test type (unit/integration)
   - Error message
   - Terraform version
   - Provider versions

## Security Testing

### tfsec

Scan for security issues:

```bash
# Install tfsec
brew install tfsec

# Scan module
tfsec infrastructure/terraform/modules/cloud-run

# Scan with specific severity
tfsec infrastructure/terraform/modules/cloud-run --minimum-severity HIGH

# Output as JSON
tfsec infrastructure/terraform/modules/cloud-run --format json
```

### Checkov

Alternative security scanner:

```bash
# Install checkov
pip install checkov

# Scan module
checkov -d infrastructure/terraform/modules/cloud-run

# Scan specific checks
checkov -d infrastructure/terraform/modules/cloud-run --check CKV_GCP_*
```

## Cost Estimation

### Infracost

Estimate infrastructure costs:

```bash
# Install infracost
brew install infracost

# Configure API key
infracost auth login

# Generate breakdown
cd cloud-run/tests/integration
infracost breakdown --path .

# Compare changes
infracost diff --path .

# Generate report
infracost breakdown --path . --format html > costs.html
```

### Manual Estimation

Each module's outputs include `estimated_monthly_cost` with:
- Per-service cost breakdown
- Free tier information
- Cost notes and warnings
- Total estimates for dev/prod

## Testing Checklist

Before submitting a PR:

- [ ] Run `./test-all.sh` locally
- [ ] All tests pass
- [ ] No format issues (`terraform fmt`)
- [ ] No security issues (`tfsec`)
- [ ] Variables have descriptions
- [ ] Variables have validation
- [ ] Outputs are documented
- [ ] README updated
- [ ] Tests documented
- [ ] Changes noted in CHANGELOG

## Advanced Testing

### Terratest

For more advanced testing with Go:

```go
package test

import (
    "testing"
    "github.com/gruntwork-io/terratest/modules/terraform"
)

func TestCloudRunModule(t *testing.T) {
    terraformOptions := &terraform.Options{
        TerraformDir: "../cloud-run/tests/integration",
    }

    defer terraform.Destroy(t, terraformOptions)
    terraform.InitAndPlan(t, terraformOptions)
}
```

### LocalStack

Test AWS resources locally:

```bash
# Start LocalStack
docker run -d -p 4566:4566 localstack/localstack

# Update provider
provider "aws" {
  endpoints {
    lambda = "http://localhost:4566"
    iam    = "http://localhost:4566"
  }
}
```

## Resources

- [Terraform Testing Best Practices](https://www.terraform.io/docs/cloud/guides/recommended-practices/part1.html)
- [Terratest Documentation](https://terratest.gruntwork.io/)
- [tfsec Documentation](https://aquasecurity.github.io/tfsec/)
- [Infracost Documentation](https://www.infracost.io/docs/)

## Support

For testing issues:
- Check module-specific `tests/README.md`
- Review GitHub Actions logs
- Search/create GitHub issues
- Contact infrastructure team
