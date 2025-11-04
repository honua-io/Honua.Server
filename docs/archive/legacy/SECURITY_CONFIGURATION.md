# Security Configuration Guide

## Overview

This guide provides comprehensive best practices for securing sensitive configuration data in Honua.Server deployments. **Never store secrets in plain text** in configuration files or source control.

---

## Issue #5: Configuration Secrets Management

### Vulnerability

Lines 94-95 and 290-295 in `HonuaConfiguration.cs` allow storing AWS S3 credentials in plain text:

```csharp
public string? AccessKeyId { get; init; }        // Line 94 (AttachmentS3StorageConfiguration)
public string? SecretAccessKey { get; init; }    // Line 95 (AttachmentS3StorageConfiguration)
public string? CogCacheS3AccessKeyId { get; init; }      // Line 290 (RasterCacheConfiguration)
public string? CogCacheS3SecretAccessKey { get; init; }  // Line 295 (RasterCacheConfiguration)
```

**Attack Scenario**: If configuration files are committed to source control, leaked via logs, or exposed through configuration dumps, attackers gain full access to your S3 buckets, potentially leading to:
- Data exfiltration
- Data tampering or deletion
- Resource exhaustion attacks (billing fraud)
- Lateral movement to other AWS services

### Secure Solutions

#### 1. Environment Variables (Recommended for Development)

**Never** hardcode credentials in `appsettings.json`. Use environment variables instead:

```bash
# Linux/macOS
export AWS_ACCESS_KEY_ID=your-key-id
export AWS_SECRET_ACCESS_KEY=your-secret-key
export AZURE_CONNECTION_STRING=your-connection-string

# Windows PowerShell
$env:AWS_ACCESS_KEY_ID="your-key-id"
$env:AWS_SECRET_ACCESS_KEY="your-secret-key"
$env:AZURE_CONNECTION_STRING="your-connection-string"
```

**appsettings.json** (reference environment variables):

```json
{
  "honua": {
    "attachments": {
      "profiles": {
        "default": {
          "provider": "s3",
          "s3": {
            "bucketName": "honua-attachments",
            "region": "us-east-1",
            "useInstanceProfile": true,
            "accessKeyId": null,
            "secretAccessKey": null
          }
        }
      }
    },
    "rasterCache": {
      "cogCacheEnabled": true,
      "cogCacheProvider": "s3",
      "cogCacheS3Bucket": "honua-cog-cache",
      "cogCacheS3Region": "us-east-1",
      "cogCacheS3AccessKeyId": null,
      "cogCacheS3SecretAccessKey": null
    }
  }
}
```

#### 2. AWS IAM Instance Profiles (Recommended for Production on AWS)

**Best Practice**: Use IAM instance profiles/roles instead of access keys.

```json
{
  "honua": {
    "attachments": {
      "profiles": {
        "default": {
          "provider": "s3",
          "s3": {
            "bucketName": "honua-attachments",
            "region": "us-east-1",
            "useInstanceProfile": true
          }
        }
      }
    }
  }
}
```

**IAM Policy Example**:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject"
      ],
      "Resource": "arn:aws:s3:::honua-attachments/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket"
      ],
      "Resource": "arn:aws:s3:::honua-attachments"
    }
  ]
}
```

#### 3. AWS Secrets Manager Integration (Recommended for Production)

**appsettings.json**:

```json
{
  "AWS": {
    "Region": "us-east-1"
  },
  "SecretsManager": {
    "Enabled": true,
    "SecretNames": {
      "S3Credentials": "honua/production/s3-credentials",
      "DatabaseConnection": "honua/production/database"
    }
  }
}
```

**Secret Structure in AWS Secrets Manager** (`honua/production/s3-credentials`):

```json
{
  "accessKeyId": "AKIA...",
  "secretAccessKey": "...",
  "region": "us-east-1"
}
```

**Program.cs Integration**:

```csharp
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from AWS Secrets Manager
if (builder.Configuration.GetValue<bool>("SecretsManager:Enabled"))
{
    var secretNames = builder.Configuration.GetSection("SecretsManager:SecretNames");
    var region = builder.Configuration["AWS:Region"] ?? "us-east-1";

    using var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));

    foreach (var secretConfig in secretNames.GetChildren())
    {
        var secretName = secretConfig.Value;
        if (string.IsNullOrEmpty(secretName)) continue;

        try
        {
            var request = new GetSecretValueRequest { SecretId = secretName };
            var response = await client.GetSecretValueAsync(request);

            // Parse secret JSON and add to configuration
            var secretData = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
            if (secretData != null)
            {
                var prefix = $"Secrets:{secretConfig.Key}";
                builder.Configuration.AddInMemoryCollection(
                    secretData.Select(kvp => new KeyValuePair<string, string?>($"{prefix}:{kvp.Key}", kvp.Value))
                );
            }
        }
        catch (Exception ex)
        {
            builder.Logging.AddConsole().Services.BuildServiceProvider()
                .GetRequiredService<ILogger<Program>>()
                .LogError(ex, "Failed to load secret {SecretName}", secretName);
        }
    }
}
```

#### 4. Azure Key Vault Integration (Recommended for Production on Azure)

**Install Package** (already included):

```bash
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

**appsettings.json**:

```json
{
  "AzureKeyVault": {
    "Enabled": true,
    "VaultUri": "https://honua-vault.vault.azure.net/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  }
}
```

**Program.cs Integration**:

```csharp
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from Azure Key Vault
if (builder.Configuration.GetValue<bool>("AzureKeyVault:Enabled"))
{
    var vaultUri = builder.Configuration["AzureKeyVault:VaultUri"];
    if (!string.IsNullOrEmpty(vaultUri))
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            // Prefer Managed Identity in production, Visual Studio in development
            ExcludeEnvironmentCredential = false,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeInteractiveBrowserCredential = true
        });

        builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), credential);
    }
}
```

**Azure Key Vault Secret Names**:

```
honua--attachments--profiles--default--azure--connectionString
honua--rasterCache--cogCacheAzureConnectionString
```

**Note**: Azure Key Vault replaces `:` with `--` in secret names.

#### 5. User Secrets (Development Only)

**Never use for production**. For local development:

```bash
dotnet user-secrets init
dotnet user-secrets set "honua:attachments:profiles:default:s3:accessKeyId" "AKIA..."
dotnet user-secrets set "honua:attachments:profiles:default:s3:secretAccessKey" "..."
```

---

## Configuration Security Checklist

- [ ] **Never commit secrets to source control**
- [ ] Add `appsettings.*.json`, `.env`, and `secrets.json` to `.gitignore`
- [ ] Use environment variables for development
- [ ] Use IAM roles/Managed Identity for production (no keys needed)
- [ ] Use Azure Key Vault or AWS Secrets Manager for multi-cloud deployments
- [ ] Rotate credentials regularly (every 90 days minimum)
- [ ] Enable secret audit logging in Key Vault/Secrets Manager
- [ ] Use least-privilege IAM policies
- [ ] Enable encryption at rest for secrets storage
- [ ] Use separate secrets for dev/staging/production environments

---

## Example: Secure Production Configuration

**appsettings.Production.json** (safe to commit):

```json
{
  "honua": {
    "attachments": {
      "profiles": {
        "default": {
          "provider": "s3",
          "s3": {
            "bucketName": "honua-production-attachments",
            "region": "us-east-1",
            "useInstanceProfile": true
          }
        }
      }
    },
    "rasterCache": {
      "cogCacheEnabled": true,
      "cogCacheProvider": "s3",
      "cogCacheS3Bucket": "honua-production-cog-cache",
      "cogCacheS3Region": "us-east-1"
    }
  },
  "AzureKeyVault": {
    "Enabled": true,
    "VaultUri": "https://honua-prod-vault.vault.azure.net/"
  }
}
```

**Environment Variables** (set via deployment pipeline):

```bash
ASPNETCORE_ENVIRONMENT=Production
AWS_REGION=us-east-1
AZURE_TENANT_ID=your-tenant-id
AZURE_CLIENT_ID=your-managed-identity-client-id
```

---

## Monitoring and Auditing

### AWS CloudTrail

Enable CloudTrail logging for Secrets Manager access:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "arn:aws:secretsmanager:*:*:secret:honua/*",
      "Condition": {
        "StringEquals": {
          "aws:RequestedRegion": "us-east-1"
        }
      }
    }
  ]
}
```

### Azure Key Vault Diagnostics

Enable diagnostic logging:

```bash
az monitor diagnostic-settings create \
  --name KeyVaultAudit \
  --resource /subscriptions/{subscription-id}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{vault-name} \
  --logs '[{"category":"AuditEvent","enabled":true}]' \
  --workspace /subscriptions/{subscription-id}/resourceGroups/{rg}/providers/Microsoft.OperationalInsights/workspaces/{workspace-name}
```

---

## Incident Response

### If Credentials Are Compromised

1. **Immediately rotate compromised credentials**
2. **Revoke old credentials in IAM/Key Vault**
3. **Review CloudTrail/Azure Activity Logs for unauthorized access**
4. **Check S3/Blob Storage access logs for data exfiltration**
5. **Update all affected environments**
6. **Conduct post-incident review**

### AWS Credential Rotation

```bash
# Create new access key
aws iam create-access-key --user-name honua-service-user

# Update secrets in Secrets Manager
aws secretsmanager update-secret \
  --secret-id honua/production/s3-credentials \
  --secret-string '{"accessKeyId":"AKIA...","secretAccessKey":"...","region":"us-east-1"}'

# Delete old access key
aws iam delete-access-key --user-name honua-service-user --access-key-id AKIA_OLD_KEY
```

### Azure Credential Rotation

```bash
# Regenerate storage account key
az storage account keys renew \
  --account-name honuastorage \
  --key primary

# Update Key Vault secret
az keyvault secret set \
  --vault-name honua-vault \
  --name honua--storage--connectionString \
  --value "DefaultEndpointsProtocol=https;AccountName=..."
```

---

## References

- [AWS Secrets Manager Best Practices](https://docs.aws.amazon.com/secretsmanager/latest/userguide/best-practices.html)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
- [CWE-798: Use of Hard-coded Credentials](https://cwe.mitre.org/data/definitions/798.html)

---

**Last Updated**: 2025-01-23
**Security Level**: CRITICAL
**Affected Components**: Configuration, S3 Storage, Azure Blob Storage, Secrets Management
