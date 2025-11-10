# IAM Setup Guide for Honua Build Orchestrator

This guide provides step-by-step instructions for setting up the IAM infrastructure required to run the Honua build orchestrator system across AWS, Azure, and GCP.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [AWS Setup](#aws-setup)
- [Azure Setup](#azure-setup)
- [GCP Setup](#gcp-setup)
- [GitHub App Setup](#github-app-setup)
- [Environment Variables](#environment-variables)
- [Security Best Practices](#security-best-practices)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)

## Overview

The Honua build orchestrator requires permissions to:

1. **Create and manage customer-specific container registries** in AWS ECR, Azure ACR, and GCP Artifact Registry
2. **Create IAM users/service principals** with scoped permissions for customer access
3. **Clone private GitHub repositories** to build customer container images
4. **Push/pull container images** to/from customer registries
5. **Access secrets** (license keys, GitHub PAT tokens) for build operations

This infrastructure uses the principle of least privilege, with scoped permissions limited to specific resource patterns.

## Prerequisites

Before starting, ensure you have:

- **AWS Account** with admin access or permissions to create IAM roles and policies
- **Azure Subscription** with Owner or User Access Administrator role
- **GCP Project** with Project IAM Admin role
- **GitHub Organization** admin access for creating GitHub Apps
- **Terraform** v1.5 or later installed
- **AWS CLI** configured with appropriate credentials
- **Azure CLI** logged in with `az login`
- **gcloud CLI** authenticated with `gcloud auth login`

## AWS Setup

### Step 1: Review IAM Policy

Review the build orchestrator IAM policy in `/home/mike/projects/HonuaIO/infrastructure/iam/aws-build-orchestrator-policy.json`. This policy grants:

- **ECR Repository Management**: Create, delete, and manage repositories under `honua/*` prefix
- **ECR Image Operations**: Push and pull images to/from customer repositories
- **IAM User Management**: Create and manage IAM users with `honua-customer-*` prefix
- **IAM Policy Management**: Create and attach policies to customer IAM users
- **Secrets Access**: Read GitHub PAT and license keys from Secrets Manager
- **KMS Decryption**: Decrypt secrets using KMS keys
- **CloudWatch Logs**: Write orchestrator activity logs

### Step 2: Deploy Infrastructure with Terraform

```bash
cd /home/mike/projects/HonuaIO/infrastructure/terraform

# Initialize Terraform
terraform init

# Review the planned changes
terraform plan \
  -var="gcp_project_id=YOUR_GCP_PROJECT_ID" \
  -var="github_org=YOUR_GITHUB_ORG" \
  -var="environment=production"

# Apply the configuration
terraform apply \
  -var="gcp_project_id=YOUR_GCP_PROJECT_ID" \
  -var="github_org=YOUR_GITHUB_ORG" \
  -var="environment=production"
```

### Step 3: Store GitHub PAT in Secrets Manager

Generate a GitHub Personal Access Token (PAT) or use the GitHub App credentials (see [GitHub App Setup](#github-app-setup)).

```bash
# Store the GitHub PAT
aws secretsmanager put-secret-value \
  --secret-id honua/github-pat-build-orchestrator \
  --secret-string "ghp_your_token_here" \
  --region us-east-1
```

### Step 4: Configure Customer IAM User Template

The customer IAM user template is located at `/home/mike/projects/HonuaIO/infrastructure/iam/aws-customer-template-policy.json`. This template is used by the build orchestrator to create customer-specific IAM users with scoped ECR access.

**Important**: This is a template file. The orchestrator will replace placeholders:
- `REGION`: AWS region (e.g., `us-east-1`)
- `ACCOUNT`: AWS account ID
- `CUSTOMER_ID`: Unique customer identifier

### Step 5: Verify IAM Role

```bash
# Verify the role was created
aws iam get-role --role-name honua-build-orchestrator

# List attached policies
aws iam list-attached-role-policies --role-name honua-build-orchestrator
```

## Azure Setup

### Step 1: Review Azure Role Definition

Review the build orchestrator role definition in `/home/mike/projects/HonuaIO/infrastructure/iam/azure-build-orchestrator-role.json`. This role grants:

- **Container Registry Management**: Create, delete, and manage ACR registries and repositories
- **Scope Map and Token Management**: Create customer-specific access tokens
- **Managed Identity Management**: Create and manage customer service principals
- **Role Assignment Management**: Assign roles to customer identities
- **Key Vault Access**: Read secrets for build operations
- **Data Actions**: Push/pull container images, delete artifacts

### Step 2: Deploy Azure Resources

The Terraform configuration (from Step 2 of AWS Setup) also deploys Azure resources. After applying, verify:

```bash
# Verify resource group
az group show --name honua-registries-production

# Verify managed identity
az identity show \
  --name honua-build-orchestrator \
  --resource-group honua-registries-production

# Verify custom role definition
az role definition list \
  --name "Honua Build Orchestrator" \
  --resource-group honua-registries-production
```

### Step 3: Store GitHub PAT in Key Vault

```bash
# Get the Key Vault name from Terraform output
KEY_VAULT_NAME=$(terraform output -raw azure_key_vault_uri | sed 's|https://||' | sed 's|.vault.azure.net/||')

# Store the GitHub PAT
az keyvault secret set \
  --vault-name "$KEY_VAULT_NAME" \
  --name github-pat \
  --value "ghp_your_token_here"
```

### Step 4: Grant Additional Permissions (if needed)

If running the orchestrator in Azure Container Instances or App Service:

```bash
# Get the managed identity principal ID
PRINCIPAL_ID=$(terraform output -raw azure_managed_identity_principal_id)

# Assign AcrPush role for pushing images
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role AcrPush \
  --scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/honua-registries-production"
```

## GCP Setup

### Step 1: Review GCP IAM Role

Review the build orchestrator role definition in `/home/mike/projects/HonuaIO/infrastructure/iam/gcp-build-orchestrator-role.yaml`. This role grants:

- **Artifact Registry Management**: Create, delete, and manage repositories
- **Image Operations**: Get, list, and tag Docker images
- **Service Account Management**: Create and manage customer service accounts
- **Service Account Keys**: Create and delete keys for customer access
- **Secret Manager**: Access build secrets (GitHub PAT, license keys)
- **Project Access**: Read project metadata

### Step 2: Deploy GCP Resources

The Terraform configuration deploys GCP resources. Verify after applying:

```bash
# Verify service account
gcloud iam service-accounts describe \
  honua-build-orchestrator@YOUR_PROJECT_ID.iam.gserviceaccount.com

# Verify custom role
gcloud iam roles describe honuaBuildOrchestrator --project=YOUR_PROJECT_ID

# List role bindings
gcloud projects get-iam-policy YOUR_PROJECT_ID \
  --flatten="bindings[].members" \
  --filter="bindings.members:honua-build-orchestrator@YOUR_PROJECT_ID.iam.gserviceaccount.com"
```

### Step 3: Store GitHub PAT in Secret Manager

```bash
# Store the GitHub PAT
echo -n "ghp_your_token_here" | gcloud secrets versions add \
  honua-github-pat-build-orchestrator \
  --data-file=- \
  --project=YOUR_PROJECT_ID
```

### Step 4: Create Service Account Key (for local testing)

```bash
# Create a key file for the service account
gcloud iam service-accounts keys create \
  ~/honua-orchestrator-sa-key.json \
  --iam-account=honua-build-orchestrator@YOUR_PROJECT_ID.iam.gserviceaccount.com

# Set environment variable for local testing
export GOOGLE_APPLICATION_CREDENTIALS=~/honua-orchestrator-sa-key.json
```

**Security Note**: Service account keys should only be used for local development. For production, use Workload Identity or Application Default Credentials.

## GitHub App Setup

### Step 1: Create GitHub App

1. Navigate to your GitHub organization settings
2. Go to **Developer settings** > **GitHub Apps** > **New GitHub App**
3. Use the manifest in `/home/mike/projects/HonuaIO/infrastructure/github/app-manifest.json` as a template:
   - **GitHub App name**: `Honua Build Orchestrator`
   - **Homepage URL**: `https://github.com/honua-io/honua`
   - **Webhook URL**: `https://build-orchestrator.honua.io/webhooks/github` (optional, not required for builds)

### Step 2: Configure Permissions

Set the following permissions:

- **Repository permissions**:
  - Contents: `Read-only` (to clone repositories)
  - Metadata: `Read-only` (to access repository information)
- **Organization permissions**:
  - Packages: `Read and write` (to push container images to GHCR if needed)

**Important**: Do not enable any webhook events unless you need real-time build triggers.

### Step 3: Install the App

1. After creating the app, install it on your organization
2. Select repositories:
   - Either "All repositories" (recommended for multi-customer builds)
   - Or select specific repositories containing customer code

### Step 4: Generate and Store Private Key

1. In the GitHub App settings, scroll to **Private keys**
2. Click **Generate a private key**
3. Download the `.pem` file
4. Store the private key securely:

**AWS:**
```bash
aws secretsmanager create-secret \
  --name honua/github-app-private-key \
  --secret-string file://path/to/downloaded-key.pem \
  --region us-east-1
```

**Azure:**
```bash
az keyvault secret set \
  --vault-name honua-orchestrator-kv \
  --name github-app-private-key \
  --file path/to/downloaded-key.pem
```

**GCP:**
```bash
gcloud secrets create honua-github-app-private-key \
  --data-file=path/to/downloaded-key.pem \
  --project=YOUR_PROJECT_ID
```

### Step 5: Note App Credentials

Record the following from the GitHub App settings page:
- **App ID**: Found at the top of the settings page
- **Installation ID**: Found in the "Install App" section after installation
- **Private Key**: Stored in secrets (from Step 4)

These will be used in environment variables (see next section).

## Environment Variables

The build orchestrator requires the following environment variables:

### AWS Configuration

```bash
# AWS IAM Role ARN (from Terraform output)
export AWS_ORCHESTRATOR_ROLE_ARN="arn:aws:iam::123456789012:role/honua-build-orchestrator"

# AWS Region for primary operations
export AWS_DEFAULT_REGION="us-east-1"

# Allowed regions for customer ECR repositories
export AWS_ALLOWED_ECR_REGIONS="us-east-1,us-west-2,eu-west-1,ap-southeast-1"
```

### Azure Configuration

```bash
# Azure Managed Identity Client ID (from Terraform output)
export AZURE_CLIENT_ID="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

# Azure Subscription ID
export AZURE_SUBSCRIPTION_ID="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

# Azure Resource Group for customer registries
export AZURE_REGISTRY_RESOURCE_GROUP="honua-registries-production"

# Azure Key Vault URL (from Terraform output)
export AZURE_KEY_VAULT_URL="https://honua-orchestrator-kv.vault.azure.net/"
```

### GCP Configuration

```bash
# GCP Project ID
export GCP_PROJECT_ID="your-project-id"

# GCP Service Account Email (from Terraform output)
export GCP_SERVICE_ACCOUNT="honua-build-orchestrator@your-project-id.iam.gserviceaccount.com"

# GCP Region for Artifact Registry
export GCP_ARTIFACT_REGISTRY_REGION="us-central1"

# Path to service account key (local development only)
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/service-account-key.json"
```

### GitHub App Configuration

```bash
# GitHub App ID (from GitHub App settings)
export GITHUB_APP_ID="123456"

# GitHub Installation ID (from GitHub App installation)
export GITHUB_INSTALLATION_ID="12345678"

# Secret names for GitHub credentials
export GITHUB_PAT_SECRET_NAME="honua/github-pat-build-orchestrator"
export GITHUB_APP_KEY_SECRET_NAME="honua/github-app-private-key"

# GitHub organization
export GITHUB_ORG="your-github-org"
```

### Build Orchestrator Configuration

```bash
# Orchestrator service endpoint
export ORCHESTRATOR_API_URL="https://build-orchestrator.honua.io"

# Customer registry naming pattern
export REGISTRY_NAMESPACE_PATTERN="honua/{customer_id}"

# License key secret prefix
export LICENSE_KEY_SECRET_PREFIX="honua/license-keys/"
```

### Example `.env` File

Create a `.env` file for local development:

```bash
# AWS
AWS_ORCHESTRATOR_ROLE_ARN=arn:aws:iam::123456789012:role/honua-build-orchestrator
AWS_DEFAULT_REGION=us-east-1
AWS_ALLOWED_ECR_REGIONS=us-east-1,us-west-2,eu-west-1,ap-southeast-1

# Azure
AZURE_CLIENT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
AZURE_SUBSCRIPTION_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
AZURE_REGISTRY_RESOURCE_GROUP=honua-registries-production
AZURE_KEY_VAULT_URL=https://honua-orchestrator-kv.vault.azure.net/

# GCP
GCP_PROJECT_ID=your-project-id
GCP_SERVICE_ACCOUNT=honua-build-orchestrator@your-project-id.iam.gserviceaccount.com
GCP_ARTIFACT_REGISTRY_REGION=us-central1
GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account-key.json

# GitHub
GITHUB_APP_ID=123456
GITHUB_INSTALLATION_ID=12345678
GITHUB_PAT_SECRET_NAME=honua/github-pat-build-orchestrator
GITHUB_APP_KEY_SECRET_NAME=honua/github-app-private-key
GITHUB_ORG=your-github-org

# Orchestrator
ORCHESTRATOR_API_URL=https://build-orchestrator.honua.io
REGISTRY_NAMESPACE_PATTERN=honua/{customer_id}
LICENSE_KEY_SECRET_PREFIX=honua/license-keys/
```

## Security Best Practices

### Principle of Least Privilege

1. **Scoped Resource Patterns**: All IAM policies use resource patterns (`honua/*`, `honua-customer-*`) to limit access to only customer-related resources.

2. **Tag-Based Conditions**: AWS policies require `ManagedBy: honua-orchestrator` tags on resources to prevent accidental management of other IAM users.

3. **Regional Restrictions**: ECR operations are limited to specific allowed regions to prevent unauthorized registry creation in other regions.

4. **Role Chaining**: Use IAM role assumption instead of long-lived access keys wherever possible.

### Secret Management

1. **No Hardcoded Secrets**: Never commit secrets to version control. Always use secret management services.

2. **Secret Rotation**: Implement automated rotation for GitHub PATs and service account keys:
   - AWS: Use Secrets Manager rotation features
   - Azure: Use Key Vault rotation policies
   - GCP: Use Secret Manager versions with automated rotation

3. **Access Logging**: Enable audit logging for all secret access:
   ```bash
   # AWS CloudTrail
   aws cloudtrail create-trail --name honua-secrets-audit

   # Azure Activity Log
   az monitor activity-log alert create --name honua-keyvault-access

   # GCP Cloud Audit Logs
   gcloud logging sinks create honua-secret-access
   ```

### Network Security

1. **VPC Endpoints** (AWS): Use VPC endpoints for ECR and Secrets Manager to avoid public internet:
   ```bash
   aws ec2 create-vpc-endpoint \
     --vpc-id vpc-xxxxx \
     --service-name com.amazonaws.us-east-1.ecr.api
   ```

2. **Private Endpoints** (Azure): Enable private endpoints for ACR and Key Vault:
   ```bash
   az network private-endpoint create \
     --name acr-private-endpoint \
     --resource-group honua-registries-production \
     --vnet-name honua-vnet \
     --subnet default \
     --private-connection-resource-id $(az acr show -n honua-acr -g honua-registries-production --query id -o tsv) \
     --group-id registry \
     --connection-name acr-connection
   ```

3. **Private Google Access** (GCP): Enable Private Google Access for VPC:
   ```bash
   gcloud compute networks subnets update default \
     --region us-central1 \
     --enable-private-ip-google-access
   ```

### Access Control

1. **Multi-Factor Authentication**: Require MFA for all admin accounts that can modify IAM policies or roles.

2. **Conditional Access** (Azure): Implement conditional access policies:
   ```bash
   az ad policy create \
     --definition @conditional-access-policy.json \
     --display-name "Honua Orchestrator MFA Required"
   ```

3. **Organization Policies** (GCP): Enforce organizational constraints:
   ```bash
   gcloud resource-manager org-policies set-policy \
     --organization=YOUR_ORG_ID \
     @organization-policy.yaml
   ```

### Monitoring and Alerting

1. **CloudWatch Alarms** (AWS):
   ```bash
   aws cloudwatch put-metric-alarm \
     --alarm-name honua-unauthorized-api-calls \
     --metric-name UnauthorizedAPICalls \
     --namespace AWS/IAM \
     --statistic Sum \
     --period 300 \
     --evaluation-periods 1 \
     --threshold 5
   ```

2. **Azure Monitor Alerts**:
   ```bash
   az monitor metrics alert create \
     --name honua-failed-auth \
     --resource-group honua-registries-production \
     --scopes $(az identity show -n honua-build-orchestrator -g honua-registries-production --query id -o tsv) \
     --condition "count failedAuthentications > 5" \
     --window-size 5m
   ```

3. **GCP Cloud Monitoring**:
   ```bash
   gcloud alpha monitoring policies create \
     --notification-channels=CHANNEL_ID \
     --display-name="Honua Failed Auth Alert" \
     --condition-display-name="Failed Auth Count" \
     --condition-threshold-value=5 \
     --condition-threshold-duration=300s
   ```

### Regular Audits

1. **Access Review**: Review IAM permissions quarterly
2. **Secret Rotation**: Rotate all secrets every 90 days
3. **Unused Resources**: Identify and remove unused IAM users and service accounts
4. **Permission Boundaries**: Implement IAM permission boundaries to limit maximum permissions

## Verification

### Test AWS Permissions

```bash
# Assume the orchestrator role
aws sts assume-role \
  --role-arn arn:aws:iam::123456789012:role/honua-build-orchestrator \
  --role-session-name test-session

# Test ECR repository creation
aws ecr create-repository \
  --repository-name honua/test-customer-123 \
  --region us-east-1

# Test Secrets Manager access
aws secretsmanager get-secret-value \
  --secret-id honua/github-pat-build-orchestrator \
  --region us-east-1

# Clean up test repository
aws ecr delete-repository \
  --repository-name honua/test-customer-123 \
  --region us-east-1 \
  --force
```

### Test Azure Permissions

```bash
# Login as managed identity (in Azure environment)
az login --identity --username $AZURE_CLIENT_ID

# Test ACR creation
az acr create \
  --name honuacustomer123 \
  --resource-group honua-registries-production \
  --sku Basic

# Test Key Vault access
az keyvault secret show \
  --vault-name honua-orchestrator-kv \
  --name github-pat

# Clean up test registry
az acr delete \
  --name honuacustomer123 \
  --resource-group honua-registries-production \
  --yes
```

### Test GCP Permissions

```bash
# Authenticate as service account
gcloud auth activate-service-account \
  --key-file=$GOOGLE_APPLICATION_CREDENTIALS

# Test Artifact Registry repository creation
gcloud artifacts repositories create test-customer-123 \
  --repository-format=docker \
  --location=us-central1 \
  --description="Test repository"

# Test Secret Manager access
gcloud secrets versions access latest \
  --secret=honua-github-pat-build-orchestrator

# Clean up test repository
gcloud artifacts repositories delete test-customer-123 \
  --location=us-central1 \
  --quiet
```

### Test GitHub App Access

```bash
# Test GitHub API access using the app
curl -H "Authorization: Bearer $GITHUB_TOKEN" \
  https://api.github.com/repos/YOUR_ORG/YOUR_REPO/contents/

# List installations
curl -H "Authorization: Bearer $GITHUB_APP_JWT" \
  https://api.github.com/app/installations
```

## Troubleshooting

### AWS Issues

**Problem**: `AccessDenied` when creating ECR repositories

**Solution**:
1. Verify the IAM role has the policy attached:
   ```bash
   aws iam list-attached-role-policies --role-name honua-build-orchestrator
   ```
2. Check the repository name matches the pattern `honua/*`
3. Verify the region is in the allowed list
4. Check CloudTrail logs for detailed error:
   ```bash
   aws cloudtrail lookup-events \
     --lookup-attributes AttributeKey=EventName,AttributeValue=CreateRepository
   ```

**Problem**: Cannot access secrets in Secrets Manager

**Solution**:
1. Verify KMS key permissions allow decryption
2. Check the secret ARN matches the pattern in the policy
3. Verify the secret exists in the correct region:
   ```bash
   aws secretsmanager describe-secret \
     --secret-id honua/github-pat-build-orchestrator \
     --region us-east-1
   ```

### Azure Issues

**Problem**: `Authorization failed` when creating ACR registries

**Solution**:
1. Verify the managed identity has the custom role assigned:
   ```bash
   az role assignment list \
     --assignee $AZURE_CLIENT_ID \
     --resource-group honua-registries-production
   ```
2. Check the resource group exists and is in the correct subscription
3. Review Azure Activity Log for detailed error:
   ```bash
   az monitor activity-log list \
     --resource-group honua-registries-production \
     --max-events 10
   ```

**Problem**: Cannot read secrets from Key Vault

**Solution**:
1. Verify the access policy grants `Get` permission:
   ```bash
   az keyvault show \
     --name honua-orchestrator-kv \
     --query properties.accessPolicies
   ```
2. Check the managed identity has access to the Key Vault network
3. Verify the secret exists:
   ```bash
   az keyvault secret list --vault-name honua-orchestrator-kv
   ```

### GCP Issues

**Problem**: `Permission denied` when creating Artifact Registry repositories

**Solution**:
1. Verify the service account has the custom role:
   ```bash
   gcloud projects get-iam-policy $GCP_PROJECT_ID \
     --flatten="bindings[].members" \
     --filter="bindings.members:honua-build-orchestrator@$GCP_PROJECT_ID.iam.gserviceaccount.com"
   ```
2. Check the custom role has the required permissions:
   ```bash
   gcloud iam roles describe honuaBuildOrchestrator --project=$GCP_PROJECT_ID
   ```
3. Review Cloud Audit Logs:
   ```bash
   gcloud logging read "protoPayload.serviceName=artifactregistry.googleapis.com" \
     --limit 10 \
     --format json
   ```

**Problem**: Cannot access Secret Manager secrets

**Solution**:
1. Verify the service account has `secretAccessor` role on the secret:
   ```bash
   gcloud secrets get-iam-policy honua-github-pat-build-orchestrator
   ```
2. Check the secret exists and has versions:
   ```bash
   gcloud secrets versions list honua-github-pat-build-orchestrator
   ```
3. Verify the Secret Manager API is enabled:
   ```bash
   gcloud services list --enabled | grep secretmanager
   ```

### GitHub App Issues

**Problem**: Cannot clone private repositories

**Solution**:
1. Verify the GitHub App is installed on the organization/repository
2. Check the app has `Contents: Read` permission
3. Generate a fresh installation access token:
   ```bash
   # Get JWT token (use a library like PyJWT)
   # Then exchange for installation token
   curl -X POST \
     -H "Authorization: Bearer $GITHUB_APP_JWT" \
     -H "Accept: application/vnd.github.v3+json" \
     https://api.github.com/app/installations/$INSTALLATION_ID/access_tokens
   ```
4. Verify the private key is valid and not expired

**Problem**: Cannot push to GitHub Container Registry

**Solution**:
1. Verify the app has `Packages: Read and write` permission
2. Check the package namespace matches the organization
3. Ensure the installation token has the correct scopes

### General Issues

**Problem**: Terraform apply fails with permission errors

**Solution**:
1. Verify you have sufficient permissions in all three cloud providers
2. Check the provider credentials are correctly configured:
   ```bash
   # AWS
   aws sts get-caller-identity

   # Azure
   az account show

   # GCP
   gcloud auth list
   ```
3. Review the Terraform state for partial resource creation

**Problem**: Environment variables not being read

**Solution**:
1. Verify `.env` file is in the correct location
2. Check the environment variable names match exactly (case-sensitive)
3. Ensure the application is configured to load `.env` files
4. Print environment variables to debug:
   ```bash
   env | grep -E '(AWS|AZURE|GCP|GITHUB)_'
   ```

## Next Steps

After completing the IAM setup:

1. **Deploy the Build Orchestrator**: Deploy the orchestrator service to your chosen platform (ECS, AKS, Cloud Run)
2. **Configure Customer Onboarding**: Set up the customer provisioning workflow
3. **Test End-to-End**: Build a test customer image through the entire pipeline
4. **Set Up Monitoring**: Configure CloudWatch, Azure Monitor, and Cloud Monitoring dashboards
5. **Document Runbooks**: Create operational runbooks for common tasks and incident response

## Support

For issues or questions:
- Review the [Honua Build Orchestrator Documentation](https://docs.honua.io/build-orchestrator)
- Check the [Troubleshooting Guide](#troubleshooting) above
- Open an issue in the [GitHub repository](https://github.com/honua-io/honua/issues)

## References

- [AWS IAM Best Practices](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)
- [Azure RBAC Documentation](https://docs.microsoft.com/en-us/azure/role-based-access-control/)
- [GCP IAM Overview](https://cloud.google.com/iam/docs/overview)
- [GitHub Apps Documentation](https://docs.github.com/en/developers/apps)
- [ECR User Guide](https://docs.aws.amazon.com/AmazonECR/latest/userguide/)
- [Azure Container Registry](https://docs.microsoft.com/en-us/azure/container-registry/)
- [GCP Artifact Registry](https://cloud.google.com/artifact-registry/docs)
