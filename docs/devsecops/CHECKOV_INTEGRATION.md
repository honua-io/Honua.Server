# Checkov Integration for AI-Generated Terraform

## Overview

Honua's AI devsecops agent automatically validates all AI-generated Terraform code using **Checkov**, an industry-standard Infrastructure-as-Code (IaC) security scanner. This gating mechanism ensures that insecure infrastructure configurations are detected and blocked before deployment.

## How It Works

### Workflow Integration

The Checkov security scan is integrated into the `GenerateInfrastructureCodeStep` of the deployment process:

```
1. ValidateDeploymentRequirements
2. GenerateInfrastructureCode
   ├── Generate Terraform files (main.tf, variables.tf, terraform.tfvars)
   ├── ✅ Run Checkov security scan ← NEW!
   └── Emit "InfrastructureGenerated" event (only if scan passes)
3. ReviewInfrastructure
4. DeployInfrastructure (terraform init/plan/apply)
```

### Gating Policy

**Blocking Severity Levels:**
- **CRITICAL** - Blocks deployment ❌
- **HIGH** - Blocks deployment ❌
- **MEDIUM** - Warning only (allows deployment) ⚠️
- **LOW** - Warning only (allows deployment) ⚠️

If any CRITICAL or HIGH severity issues are found, the deployment process stops immediately and provides a detailed error message with:
- List of all critical/high issues
- Check IDs and descriptions
- Affected resources and file paths
- Remediation guidance

## Installation

### Prerequisites

Checkov requires Python 3.8 or higher.

### Install Checkov

```bash
# Using pip
pip install checkov

# Using pip3 (if you have multiple Python versions)
pip3 install checkov

# Verify installation
checkov --version
```

### Install via Docker (Alternative)

If you don't want to install Python/pip:

```bash
# Pull the Checkov Docker image
docker pull bridgecrew/checkov:latest

# Create an alias for convenience
alias checkov='docker run --rm -v $(pwd):/tf bridgecrew/checkov:latest'
```

## Usage

### Automatic Scanning

Checkov runs automatically during infrastructure code generation. No user action required.

```csharp
// When you trigger deployment, Checkov validation happens automatically
var deploymentProcess = DeploymentProcess.BuildProcess();
await deploymentProcess.StartAsync("StartDeployment", deploymentState);

// If Checkov finds issues, an exception is thrown with details
```

### Example Output (Success)

```
[INFO] Running Checkov security scan on generated Terraform code at /tmp/honua-terraform/abc123
[INFO] Checkov scan completed: 47 passed, 0 failed (0 critical, 0 high, 0 medium, 0 low), 3 skipped
[INFO] Checkov security scan passed with no issues found.
[INFO] Generated infrastructure code for deployment abc123. Estimated cost: $45.00/month
```

### Example Output (Blocked)

```
[INFO] Running Checkov security scan on generated Terraform code at /tmp/honua-terraform/abc123
[INFO] Checkov scan completed: 42 passed, 5 failed (2 critical, 3 high, 0 medium, 0 low), 3 skipped
[ERROR] Found 2 CRITICAL severity issues. Deployment blocked.
[WARN] [CRITICAL] CKV_AWS_20: S3 Bucket has an ACL defined which allows public READ access in aws_s3_bucket.data_bucket (main.tf)
[WARN] [CRITICAL] CKV_AWS_19: Ensure all data stored in S3 is securely encrypted at rest in aws_s3_bucket.data_bucket (main.tf)
[WARN] [HIGH] CKV_AWS_23: Ensure every security group has an explicit description in aws_security_group.app_sg (main.tf)
[ERROR] Checkov found critical security issues. Blocking deployment.

InvalidOperationException: Checkov security scan found 2 CRITICAL and 3 HIGH severity issues.

Critical/High severity issues:
  [CRITICAL] CKV_AWS_20: S3 Bucket has an ACL defined which allows public READ access
    Resource: aws_s3_bucket.data_bucket
    File: main.tf

  [CRITICAL] CKV_AWS_19: Ensure all data stored in S3 is securely encrypted at rest
    Resource: aws_s3_bucket.data_bucket
    File: main.tf

  [HIGH] CKV_AWS_23: Ensure every security group has an explicit description
    Resource: aws_security_group.app_sg
    File: main.tf

Deployment blocked. Fix these security issues before proceeding.
For details, run: checkov -d <terraform-dir> --framework terraform
```

## Common Security Issues Detected

### AWS

| Check ID | Description | Severity |
|----------|-------------|----------|
| CKV_AWS_20 | S3 bucket has public ACL | CRITICAL |
| CKV_AWS_19 | S3 bucket encryption at rest | CRITICAL |
| CKV_AWS_18 | S3 bucket logging | HIGH |
| CKV_AWS_21 | S3 bucket versioning | HIGH |
| CKV_AWS_23 | Security group description | HIGH |
| CKV_AWS_24 | Security group rules | HIGH |
| CKV_AWS_46 | EBS volume encryption | HIGH |
| CKV_AWS_79 | RDS instance encryption | HIGH |

### Azure

| Check ID | Description | Severity |
|----------|-------------|----------|
| CKV_AZURE_35 | Storage account encryption | CRITICAL |
| CKV_AZURE_43 | Storage account secure transfer | HIGH |
| CKV_AZURE_33 | Storage account network rules | HIGH |
| CKV_AZURE_44 | PostgreSQL connection throttling | HIGH |
| CKV_AZURE_28 | PostgreSQL SSL enforcement | CRITICAL |

### GCP

| Check ID | Description | Severity |
|----------|-------------|----------|
| CKV_GCP_29 | Storage bucket encryption | CRITICAL |
| CKV_GCP_62 | Storage bucket uniform access | HIGH |
| CKV_GCP_6 | SQL instance backup | HIGH |
| CKV_GCP_14 | SQL instance SSL | CRITICAL |

## Manual Scanning

You can also run Checkov manually on generated Terraform code:

```bash
# Find the Terraform workspace path (logged during generation)
cd /tmp/honua-terraform/<deployment-id>

# Run Checkov scan
checkov -d . --framework terraform

# Run with specific severity threshold
checkov -d . --framework terraform --check CRITICAL,HIGH

# Output to SARIF for GitHub integration
checkov -d . --framework terraform -o sarif --output-file-path results.sarif
```

## Troubleshooting

### Checkov Not Found

**Symptoms:**
```
[WARN] Checkov is not installed. Skipping security scan. Install with: pip install checkov
```

**Solution:**
```bash
pip install checkov
# or
pip3 install checkov
```

### Checkov Scan Timeout

**Symptoms:**
```
[ERROR] Checkov scan failed due to unexpected error. Failing safely by blocking deployment.
```

**Solution:**
- Check that Checkov is properly installed
- Verify the Terraform files are valid
- Run Checkov manually to see detailed error output

### False Positives

If Checkov flags an issue that doesn't apply to your use case, you can suppress specific checks using inline comments:

```hcl
resource "aws_s3_bucket" "example" {
  bucket = "my-bucket"

  # checkov:skip=CKV_AWS_20:This bucket intentionally has public read access for static website hosting
  acl    = "public-read"
}
```

Or suppress at the file level:

```hcl
# checkov:skip=CKV_AWS_20:All buckets in this file are for public website hosting
```

## Configuration

### Custom Severity Policy (Future Enhancement)

Future versions may support custom severity policies via configuration:

```json
{
  "checkov": {
    "enabled": true,
    "blockingSeverities": ["CRITICAL", "HIGH"],
    "warningSeverities": ["MEDIUM"],
    "ignoredChecks": ["CKV_AWS_20"],
    "frameworkVersion": "latest"
  }
}
```

### Disable Checkov (Not Recommended)

To temporarily disable Checkov scanning (for testing only):

1. Comment out the validation call in `GenerateInfrastructureCodeStep.cs`:

```csharp
// SECURITY: Run Checkov security scan on generated Terraform code
// await ValidateWithCheckovAsync(workspacePath);
```

2. Rebuild the project

**⚠️ WARNING**: Disabling security scanning is strongly discouraged in production environments.

## Best Practices

1. **Keep Checkov Updated**: Run `pip install --upgrade checkov` regularly
2. **Review All Warnings**: Even non-blocking MEDIUM/LOW issues should be reviewed
3. **Document Suppressions**: Always include reason when suppressing checks
4. **Test Before Production**: Test AI-generated infrastructure in dev/staging first
5. **Monitor Trends**: Track which security issues are most common in AI-generated code
6. **Automate**: Integrate Checkov into CI/CD pipelines for manual Terraform code too

## Integration with CI/CD

### GitHub Actions

```yaml
- name: Run Checkov on AI-generated Terraform
  uses: bridgecrewio/checkov-action@v12
  with:
    directory: terraform/
    framework: terraform
    soft_fail: false
    output_format: sarif

- name: Upload SARIF to GitHub Security
  uses: github/codeql-action/upload-sarif@v2
  with:
    sarif_file: results.sarif
```

## Related Documentation

- [Checkov Documentation](https://www.checkov.io/documentation.html)
- [Checkov Policy Index](https://www.checkov.io/5.Policy%20Index/terraform.html)
- [Terraform Best Practices](../architecture/terraform-best-practices.md)
- [Security Policy](../../SECURITY.md)
- [AI Agent Architecture](../architecture/ai-agents.md)

## Support

For issues with Checkov integration:
1. Check Checkov is installed: `checkov --version`
2. Review logs in the deployment output
3. Run Checkov manually on the generated Terraform
4. Report issues at: https://github.com/honua-io/Honua.Server/issues

## Version History

- **v1.0** (2025-01-11): Initial Checkov integration with CRITICAL/HIGH blocking policy
