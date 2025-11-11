# Checkov Quick Start - AI DevSecOps

## 🎯 What This Does

Your AI devsecops agent now **automatically scans all AI-generated Terraform code** with Checkov before deployment. If critical security issues are found, deployment is **blocked** until issues are fixed.

## ⚡ Quick Setup

### 1. Install Checkov

```bash
pip install checkov
checkov --version
```

### 2. That's It!

The integration is **automatic**. Next time the AI generates infrastructure code, Checkov will:
- ✅ Scan the generated Terraform files
- ✅ Report any security issues
- ❌ **Block deployment** if CRITICAL or HIGH severity issues found
- ⚠️ Warn about MEDIUM/LOW issues but allow deployment

## 🚦 Gating Policy

| Severity | Action | Example |
|----------|--------|---------|
| **CRITICAL** | ❌ **Blocks** | S3 bucket with public ACL |
| **HIGH** | ❌ **Blocks** | Unencrypted RDS instance |
| **MEDIUM** | ⚠️ Warns | Missing security group description |
| **LOW** | ⚠️ Warns | Logging not configured |

## 📋 Example: Blocked Deployment

```
[ERROR] Checkov found critical security issues. Blocking deployment.

InvalidOperationException: Checkov security scan found 1 CRITICAL and 2 HIGH severity issues.

Critical/High severity issues:
  [CRITICAL] CKV_AWS_19: Ensure all data stored in S3 is securely encrypted at rest
    Resource: aws_s3_bucket.data_bucket
    File: main.tf

  [HIGH] CKV_AWS_46: Ensure EBS volumes are encrypted
    Resource: aws_instance.app_server
    File: main.tf

Deployment blocked. Fix these security issues before proceeding.
```

## 🔧 What Gets Scanned

- **AWS**: ECS, Lambda, RDS, S3, EC2, VPC, Security Groups
- **Azure**: Container Apps, Functions, PostgreSQL, Storage
- **GCP**: Cloud Run, Cloud Functions, Cloud SQL, Storage

All generated `main.tf`, `variables.tf`, and `terraform.tfvars` files are scanned.

## 🛠️ Manual Scan

Want to scan Terraform code manually?

```bash
# Find the workspace path (shown in logs during generation)
cd /tmp/honua-terraform/<deployment-id>

# Run Checkov
checkov -d . --framework terraform

# Only show HIGH/CRITICAL
checkov -d . --framework terraform --check HIGH,CRITICAL
```

## 🎓 Common Issues & Fixes

### Issue: S3 Bucket Not Encrypted

```
[CRITICAL] CKV_AWS_19: Ensure all data stored in S3 is securely encrypted at rest
```

**Fix**: The AI should regenerate with encryption enabled. If not, add encryption to the S3 bucket resource.

### Issue: Public Security Group

```
[HIGH] CKV_AWS_260: Ensure no security groups allow ingress from 0.0.0.0/0 to port 22
```

**Fix**: The AI should restrict SSH access. Regenerate with proper CIDR blocks.

### Issue: No Backup Enabled

```
[HIGH] CKV_AWS_133: Ensure RDS has backup enabled
```

**Fix**: The AI should enable automated backups. Regenerate with backup configuration.

## 📚 Full Documentation

For complete details, see [CHECKOV_INTEGRATION.md](./CHECKOV_INTEGRATION.md)

## 🚨 Troubleshooting

### Checkov Not Found

```
[WARN] Checkov is not installed. Skipping security scan.
```

**Solution**: `pip install checkov`

### Want to Skip Scanning? (Not Recommended)

Only for testing/development:

1. Temporarily disable by commenting out the validation in `GenerateInfrastructureCodeStep.cs`
2. **DO NOT** disable in production

## ✅ Best Practices

1. ✅ **Keep Checkov updated**: `pip install --upgrade checkov`
2. ✅ **Review all warnings**: Even non-blocking issues matter
3. ✅ **Test in dev first**: Always test AI-generated infrastructure in dev/staging
4. ✅ **Monitor trends**: Track which issues are common in AI-generated code
5. ✅ **Trust the gate**: If Checkov blocks, there's a security reason

## 🔗 Resources

- [Checkov Policy Index](https://www.checkov.io/5.Policy%20Index/terraform.html)
- [AWS Security Best Practices](https://aws.amazon.com/security/best-practices/)
- [Azure Security Baseline](https://learn.microsoft.com/en-us/security/benchmark/azure/)
- [GCP Security Best Practices](https://cloud.google.com/security/best-practices)

---

**Your AI generates infrastructure. Checkov ensures it's secure. 🔒**
