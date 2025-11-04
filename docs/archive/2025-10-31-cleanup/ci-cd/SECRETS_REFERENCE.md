# CI/CD Secrets and Variables Reference

This document provides a comprehensive reference for all secrets and variables required for CI/CD pipelines.

## Quick Reference Table

| Secret/Variable | Required | Platform | Environment | Description |
|----------------|----------|----------|-------------|-------------|
| `GITHUB_TOKEN` | Yes | GitHub | All | Automatic, used for GHCR |
| `AWS_ACCESS_KEY_ID` | Conditional | All | All | AWS credentials for ECR/EKS |
| `AWS_SECRET_ACCESS_KEY` | Conditional | All | All | AWS secret key |
| `AZURE_CREDENTIALS` | Conditional | All | Dev/Staging | Azure service principal JSON |
| `GCP_SERVICE_ACCOUNT_KEY` | Conditional | All | All | GCP service account key JSON |
| `SLACK_WEBHOOK_URL` | Optional | All | All | Slack notifications webhook |

## GitHub Actions Secrets

### Required Secrets

#### Container Registry Access

**GITHUB_TOKEN**
- **Type:** Automatic
- **Description:** Provided automatically by GitHub Actions for GHCR access
- **Required:** Yes
- **Format:** Token (automatic)
- **Usage:** Pushing images to ghcr.io

### Optional Container Registries

#### AWS ECR

Enable by setting `ENABLE_AWS_ECR=true`

**AWS_ACCESS_KEY_ID**
- **Type:** Secret
- **Description:** AWS access key for ECR authentication
- **Required:** If AWS ECR enabled
- **Format:** `AKIAIOSFODNN7EXAMPLE`
- **How to get:**
```bash
# Create IAM user with ECR permissions
aws iam create-user --user-name honua-ecr-user

# Create access key
aws iam create-access-key --user-name honua-ecr-user

# Attach ECR policy
aws iam attach-user-policy --user-name honua-ecr-user \
  --policy-arn arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryPowerUser
```

**AWS_SECRET_ACCESS_KEY**
- **Type:** Secret
- **Description:** AWS secret access key
- **Required:** If AWS ECR enabled
- **Format:** `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`
- **How to get:** Retrieved when creating access key (above)

**AWS_ECR_ALIAS**
- **Type:** Secret
- **Description:** AWS ECR Public registry alias
- **Required:** If using ECR Public
- **Format:** `your-alias`
- **How to get:**
```bash
# Get your ECR Public alias
aws ecr-public describe-registries --region us-east-1
```

#### Azure ACR

Enable by setting `ENABLE_AZURE_ACR=true`

**AZURE_REGISTRY_NAME**
- **Type:** Secret
- **Description:** Azure Container Registry name
- **Required:** If Azure ACR enabled
- **Format:** `myregistry` (without .azurecr.io)
- **How to get:**
```bash
# List your ACR registries
az acr list --query "[].{Name:name}" --output table
```

**AZURE_REGISTRY_USERNAME**
- **Type:** Secret
- **Description:** ACR admin username
- **Required:** If Azure ACR enabled
- **Format:** Username string
- **How to get:**
```bash
# Enable admin user and get credentials
az acr update -n myregistry --admin-enabled true
az acr credential show -n myregistry
```

**AZURE_REGISTRY_PASSWORD**
- **Type:** Secret
- **Description:** ACR admin password
- **Required:** If Azure ACR enabled
- **Format:** Password string
- **How to get:** Retrieved with command above

#### GCP GCR

Enable by setting `ENABLE_GCP_GCR=true`

**GCP_PROJECT_ID**
- **Type:** Secret
- **Description:** GCP project ID
- **Required:** If GCP GCR enabled
- **Format:** `my-project-id`
- **How to get:**
```bash
# List your GCP projects
gcloud projects list

# Get current project
gcloud config get-value project
```

**GCP_SERVICE_ACCOUNT_KEY**
- **Type:** Secret
- **Description:** GCP service account key in JSON format
- **Required:** If GCP GCR enabled
- **Format:** JSON
- **How to get:**
```bash
# Create service account
gcloud iam service-accounts create honua-gcr \
  --display-name="Honua GCR Service Account"

# Grant GCR permissions
gcloud projects add-iam-policy-binding PROJECT_ID \
  --member="serviceAccount:honua-gcr@PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/storage.admin"

# Create key
gcloud iam service-accounts keys create key.json \
  --iam-account=honua-gcr@PROJECT_ID.iam.gserviceaccount.com

# Use content of key.json as secret
```

### Cloud Provider Credentials

#### AWS

**AWS_REGION**
- **Type:** Variable
- **Description:** AWS region for resources
- **Required:** If using AWS
- **Format:** `us-east-1`, `eu-west-1`, etc.
- **Default:** `us-east-1`

**AWS_EKS_CLUSTER_NAME_DEV**
- **Type:** Variable
- **Description:** EKS cluster name for development
- **Required:** If deploying to AWS dev
- **Format:** `honua-dev`

**AWS_EKS_CLUSTER_NAME_STAGING**
- **Type:** Variable
- **Description:** EKS cluster name for staging
- **Required:** If deploying to AWS staging
- **Format:** `honua-staging`

**AWS_EKS_CLUSTER_NAME_PROD**
- **Type:** Variable
- **Description:** EKS cluster name for production
- **Required:** If deploying to AWS production
- **Format:** `honua-production`

**AWS_PRODUCTION_ROLE_ARN**
- **Type:** Secret
- **Description:** IAM role ARN for production deployments (enhanced security)
- **Required:** Recommended for production
- **Format:** `arn:aws:iam::123456789012:role/honua-production-deployer`
- **How to get:**
```bash
# Create production deployment role
aws iam create-role --role-name honua-production-deployer \
  --assume-role-policy-document file://trust-policy.json

# Attach policies
aws iam attach-role-policy --role-name honua-production-deployer \
  --policy-arn arn:aws:iam::aws:policy/AmazonEKSClusterPolicy
```

#### Azure

**AZURE_CREDENTIALS**
- **Type:** Secret
- **Description:** Azure service principal credentials in JSON format
- **Required:** If using Azure dev/staging
- **Format:** JSON
- **How to get:**
```bash
# Create service principal
az ad sp create-for-rbac \
  --name honua-cicd \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/{resource-group} \
  --sdk-auth

# Output will be JSON to use as secret
```

**AZURE_CREDENTIALS_PRODUCTION**
- **Type:** Secret
- **Description:** Separate service principal for production
- **Required:** Recommended for production
- **Format:** JSON
- **How to get:** Same as above, but with production scope

**AZURE_RESOURCE_GROUP_DEV**
- **Type:** Variable
- **Description:** Azure resource group for development
- **Required:** If deploying to Azure dev
- **Format:** `honua-dev-rg`

**AZURE_RESOURCE_GROUP_STAGING**
- **Type:** Variable
- **Description:** Azure resource group for staging
- **Required:** If deploying to Azure staging
- **Format:** `honua-staging-rg`

**AZURE_RESOURCE_GROUP_PROD**
- **Type:** Variable
- **Description:** Azure resource group for production
- **Required:** If deploying to Azure production
- **Format:** `honua-prod-rg`

**AZURE_AKS_CLUSTER_NAME_DEV**
- **Type:** Variable
- **Description:** AKS cluster name for development
- **Required:** If deploying to Azure dev
- **Format:** `honua-dev-aks`

**AZURE_AKS_CLUSTER_NAME_STAGING**
- **Type:** Variable
- **Description:** AKS cluster name for staging
- **Required:** If deploying to Azure staging
- **Format:** `honua-staging-aks`

**AZURE_AKS_CLUSTER_NAME_PROD**
- **Type:** Variable
- **Description:** AKS cluster name for production
- **Required:** If deploying to Azure production
- **Format:** `honua-prod-aks`

**AZURE_KEYVAULT_NAME_DEV**
- **Type:** Variable
- **Description:** Azure Key Vault name for development secrets
- **Required:** If using Azure dev
- **Format:** `honua-dev-kv`

**AZURE_KEYVAULT_NAME_STAGING**
- **Type:** Variable
- **Description:** Azure Key Vault name for staging secrets
- **Required:** If using Azure staging
- **Format:** `honua-staging-kv`

**AZURE_KEYVAULT_NAME_PROD**
- **Type:** Variable
- **Description:** Azure Key Vault name for production secrets
- **Required:** If using Azure production
- **Format:** `honua-prod-kv`

#### GCP

**GCP_REGION**
- **Type:** Variable
- **Description:** GCP region for resources
- **Required:** If using GCP
- **Format:** `us-central1`, `europe-west1`, etc.
- **Default:** `us-central1`

**GCP_GKE_CLUSTER_NAME_DEV**
- **Type:** Variable
- **Description:** GKE cluster name for development
- **Required:** If deploying to GCP dev
- **Format:** `honua-dev-gke`

**GCP_GKE_CLUSTER_NAME_STAGING**
- **Type:** Variable
- **Description:** GKE cluster name for staging
- **Required:** If deploying to GCP staging
- **Format:** `honua-staging-gke`

**GCP_GKE_CLUSTER_NAME_PROD**
- **Type:** Variable
- **Description:** GKE cluster name for production
- **Required:** If deploying to GCP production
- **Format:** `honua-prod-gke`

**GCP_SERVICE_ACCOUNT_KEY_PRODUCTION**
- **Type:** Secret
- **Description:** Separate service account for production
- **Required:** Recommended for production
- **Format:** JSON
- **How to get:** Same as GCP_SERVICE_ACCOUNT_KEY but with production permissions

### Application Secrets

**STAGING_API_KEY**
- **Type:** Secret
- **Description:** API key for staging environment testing
- **Required:** If running integration tests in staging
- **Format:** Random string (generate with `openssl rand -base64 32`)

**PRODUCTION_API_KEY**
- **Type:** Secret
- **Description:** API key for production environment
- **Required:** If running smoke tests in production
- **Format:** Random string (generate with `openssl rand -base64 32`)

### Notification Services

**SLACK_WEBHOOK_URL**
- **Type:** Secret
- **Description:** Slack webhook URL for notifications
- **Required:** If ENABLE_SLACK_NOTIFICATIONS=true
- **Format:** `https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX`
- **How to get:**
  1. Go to https://api.slack.com/messaging/webhooks
  2. Create incoming webhook for your workspace
  3. Select channel for notifications
  4. Copy webhook URL

**TEAMS_WEBHOOK_URL**
- **Type:** Secret
- **Description:** Microsoft Teams webhook URL for notifications
- **Required:** If ENABLE_TEAMS_NOTIFICATIONS=true
- **Format:** `https://outlook.office.com/webhook/...`
- **How to get:**
  1. Open Teams channel
  2. Click ••• → Connectors
  3. Add "Incoming Webhook"
  4. Configure and copy URL

### Optional Services

**COSIGN_PRIVATE_KEY**
- **Type:** Secret
- **Description:** Cosign private key for image signing
- **Required:** If ENABLE_IMAGE_SIGNING=true
- **Format:** PEM format private key
- **How to get:**
```bash
# Generate Cosign key pair
cosign generate-key-pair

# Use content of cosign.key as secret
```

**COSIGN_PASSWORD**
- **Type:** Secret
- **Description:** Password for Cosign private key
- **Required:** If ENABLE_IMAGE_SIGNING=true
- **Format:** Password string
- **How to get:** Set when generating key pair

**HELM_REPO_URL**
- **Type:** Secret
- **Description:** Helm chart repository URL
- **Required:** If ENABLE_HELM_PUBLISH=true
- **Format:** `https://charts.example.com`

**HELM_REPO_USERNAME**
- **Type:** Secret
- **Description:** Helm repository username
- **Required:** If ENABLE_HELM_PUBLISH=true
- **Format:** Username string

**HELM_REPO_PASSWORD**
- **Type:** Secret
- **Description:** Helm repository password
- **Required:** If ENABLE_HELM_PUBLISH=true
- **Format:** Password string

**SONAR_TOKEN**
- **Type:** Secret
- **Description:** SonarCloud/SonarQube authentication token
- **Required:** If using SonarCloud analysis
- **Format:** Token string
- **How to get:**
  1. Go to SonarCloud → My Account → Security
  2. Generate new token
  3. Copy token value

**PAT_TOKEN**
- **Type:** Secret
- **Description:** GitHub Personal Access Token for releases
- **Required:** If creating releases with additional permissions
- **Format:** `ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- **How to get:**
  1. GitHub Settings → Developer settings → Personal access tokens
  2. Generate new token with `repo` and `write:packages` scopes

## GitHub Variables

Configure in Settings → Secrets and Variables → Actions → Variables

**ENABLE_AWS_ECR**
- **Type:** Boolean string
- **Description:** Enable AWS ECR push
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

**ENABLE_AZURE_ACR**
- **Type:** Boolean string
- **Description:** Enable Azure ACR push
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

**ENABLE_GCP_GCR**
- **Type:** Boolean string
- **Description:** Enable GCP GCR push
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

**ENABLE_CLOUD_RUN**
- **Type:** Boolean string
- **Description:** Enable GCP Cloud Run deployment
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

**ENABLE_IMAGE_SIGNING**
- **Type:** Boolean string
- **Description:** Enable container image signing with Cosign
- **Values:** `'true'` or `'false'`
- **Default:** `'true'`

**ENABLE_MAINTENANCE_MODE**
- **Type:** Boolean string
- **Description:** Enable maintenance mode during deployments
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

**ENABLE_SLACK_NOTIFICATIONS**
- **Type:** Boolean string
- **Description:** Enable Slack notifications
- **Values:** `'true'` or `'false'`
- **Default:** `'true'`

**ENABLE_TEAMS_NOTIFICATIONS**
- **Type:** Boolean string
- **Description:** Enable Microsoft Teams notifications
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

**ENABLE_HELM_PUBLISH**
- **Type:** Boolean string
- **Description:** Enable Helm chart publishing
- **Values:** `'true'` or `'false'`
- **Default:** `'false'`

## Security Best Practices

### Secret Management

1. **Never commit secrets to Git**
   - Use `.gitignore` for local credential files
   - Scan commits for secrets with tools like git-secrets

2. **Rotate secrets regularly**
   - Set calendar reminders for quarterly rotation
   - Update all secrets after team member departures

3. **Use minimal permissions**
   - Grant least privilege necessary
   - Use separate service principals/accounts per environment

4. **Encrypt secrets at rest**
   - Use GitHub's encrypted secrets
   - Use cloud provider secret managers

5. **Audit secret access**
   - Review GitHub Actions logs
   - Enable audit logging in cloud providers

### Access Control

1. **Repository settings**
   ```yaml
   # Required: Enable branch protection
   # Settings → Branches → Add rule
   - Require pull request reviews
   - Require status checks to pass
   - Require signed commits
   - Restrict who can push to matching branches
   ```

2. **Environment protection**
   ```yaml
   # Settings → Environments → Protection rules
   - Required reviewers (for production)
   - Wait timer (for staging)
   - Deployment branches (limit to specific branches)
   ```

3. **Secret access**
   ```yaml
   # Limit secret access to specific workflows
   # Secrets → Configure secret → Repository access
   ```

## Validation

### Test Secrets Locally

**AWS:**
```bash
# Test AWS credentials
aws sts get-caller-identity

# Test ECR access
aws ecr get-login-password --region us-east-1
```

**Azure:**
```bash
# Test Azure credentials
az login --service-principal \
  -u <client-id> \
  -p <client-secret> \
  --tenant <tenant-id>

# Test ACR access
az acr login --name myregistry
```

**GCP:**
```bash
# Test service account
gcloud auth activate-service-account --key-file=key.json

# Test GCR access
gcloud auth configure-docker gcr.io
```

**Kubernetes Access:**
```bash
# Test EKS
aws eks update-kubeconfig --region us-east-1 --name honua-dev
kubectl get nodes

# Test AKS
az aks get-credentials --resource-group honua-dev-rg --name honua-dev-aks
kubectl get nodes

# Test GKE
gcloud container clusters get-credentials honua-dev-gke --region us-central1
kubectl get nodes
```

## Troubleshooting

### Common Issues

**Issue:** Secret not found
```
Error: Secret AWS_ACCESS_KEY_ID not set
```
**Solution:** Verify secret is configured in repository settings

**Issue:** Invalid credentials
```
Error: 403 Forbidden
```
**Solution:** Verify credentials are correct and have necessary permissions

**Issue:** Region mismatch
```
Error: Cluster not found in region
```
**Solution:** Verify AWS_REGION matches cluster region

**Issue:** Service principal expired
```
Error: AADSTS7000222: The provided client secret keys are expired
```
**Solution:** Create new service principal or renew secret

---

**Last Updated:** $(date)
**Maintained By:** DevOps Team
