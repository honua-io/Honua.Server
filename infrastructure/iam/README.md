# Honua Build Orchestrator - IAM Policies

This directory contains IAM policy definitions for the Honua build orchestrator system across AWS, Azure, and GCP.

## Overview

The build orchestrator requires permissions to provision and manage customer-specific container registries and access credentials across multiple cloud providers. These policies implement the principle of least privilege with resource-scoped permissions.

## Files

### AWS Policies

#### `aws-build-orchestrator-policy.json`

IAM policy for the build orchestrator service to manage customer resources in AWS.

**Key Permissions:**
- **ECR Repository Management**: Create, delete, and configure repositories under `honua/*` namespace
- **ECR Image Operations**: Push and pull container images
- **IAM User Management**: Create customer IAM users with `honua-customer-*` prefix
- **IAM Policy Management**: Create and attach customer-specific policies
- **Access Key Management**: Generate access keys for customer IAM users
- **Secrets Manager**: Read GitHub PAT and license keys
- **KMS**: Decrypt secrets encrypted with KMS
- **CloudWatch Logs**: Write orchestrator activity logs

**Resource Patterns:**
- ECR Repositories: `arn:aws:ecr:*:*:repository/honua/*`
- IAM Users: `arn:aws:iam::*:user/honua-customer-*`
- IAM Policies: `arn:aws:iam::*:policy/honua-customer-*`
- Secrets: `arn:aws:secretsmanager:*:*:secret:honua/github-pat-*`, `arn:aws:secretsmanager:*:*:secret:honua/license-keys/*`

**Security Features:**
- Regional restrictions via condition keys
- Tag-based resource filtering (`ManagedBy: honua-orchestrator`)
- Scoped policy attachment (only `honua-customer-*` policies can be attached)

#### `aws-customer-template-policy.json`

Template IAM policy for customer users to access their specific ECR repository.

**Placeholders:**
- `REGION`: AWS region (e.g., `us-east-1`)
- `ACCOUNT`: AWS account ID
- `CUSTOMER_ID`: Unique customer identifier

**Permissions:**
- Pull images from customer-specific repository
- Push images to customer-specific repository
- Get ECR authorization token

**Usage:**
The build orchestrator dynamically generates customer policies by replacing placeholders with actual values during customer provisioning.

### Azure Policies

#### `azure-build-orchestrator-role.json`

Custom Azure RBAC role definition for the build orchestrator managed identity.

**Key Permissions:**
- **Container Registry**: Full management of ACR registries and repositories
- **Scope Maps and Tokens**: Create customer-specific access tokens
- **Managed Identities**: Create and manage customer service principals
- **Role Assignments**: Assign roles to customer identities
- **Key Vault**: Read secrets for build operations

**Data Actions:**
- Push/pull container images
- Delete artifacts

**Assignable Scopes:**
- Resource group: `/subscriptions/SUBSCRIPTION_ID/resourceGroups/honua-registries`

**Usage:**
Deploy using Azure CLI:
```bash
az role definition create --role-definition @azure-build-orchestrator-role.json
```

### GCP Policies

#### `gcp-build-orchestrator-role.yaml`

Custom GCP IAM role for the build orchestrator service account.

**Key Permissions:**
- **Artifact Registry**: Repository and image management
- **Service Accounts**: Create and manage customer service accounts
- **Service Account Keys**: Generate keys for customer access
- **Secret Manager**: Read build secrets
- **Project Metadata**: Read project information

**Usage:**
Deploy using gcloud CLI:
```bash
gcloud iam roles create honuaBuildOrchestrator \
  --project=YOUR_PROJECT_ID \
  --file=gcp-build-orchestrator-role.yaml
```

## Security Considerations

### Resource Scoping

All policies use strict resource patterns to limit access:

- **AWS**: Resources must match `honua/*` or `honua-customer-*` patterns
- **Azure**: Operations scoped to specific resource group
- **GCP**: Custom role applied at project level with resource-specific permissions

### Tag-Based Access Control

AWS policies use resource tags to enforce management boundaries:

```json
"Condition": {
  "StringEquals": {
    "iam:ResourceTag/ManagedBy": "honua-orchestrator"
  }
}
```

This ensures the orchestrator can only manage resources it created.

### Regional Restrictions

AWS policies limit ECR operations to approved regions:

```json
"Condition": {
  "StringEquals": {
    "aws:RequestedRegion": [
      "us-east-1",
      "us-west-2",
      "eu-west-1",
      "ap-southeast-1"
    ]
  }
}
```

### Secrets Access

Access to secrets is limited by resource ARN/name patterns:

- **AWS**: `honua/github-pat-*`, `honua/license-keys/*`
- **Azure**: Key Vault access policy with specific secret permissions
- **GCP**: Secret Manager with `secretAccessor` role on specific secrets

## Deployment

### Prerequisites

- AWS account with IAM admin permissions
- Azure subscription with Owner or User Access Administrator role
- GCP project with Project IAM Admin role
- Terraform v1.5 or later (recommended method)

### Option 1: Terraform (Recommended)

Deploy all infrastructure using the provided Terraform configuration:

```bash
cd ../terraform
terraform init
terraform plan -var="gcp_project_id=YOUR_PROJECT_ID" -var="github_org=YOUR_ORG"
terraform apply -var="gcp_project_id=YOUR_PROJECT_ID" -var="github_org=YOUR_ORG"
```

See [`/home/mike/projects/HonuaIO/docs/IAM_SETUP.md`](/home/mike/projects/HonuaIO/docs/IAM_SETUP.md) for detailed instructions.

### Option 2: Manual Deployment

#### AWS

```bash
# Create the IAM policy
aws iam create-policy \
  --policy-name honua-build-orchestrator-policy \
  --policy-document file://aws-build-orchestrator-policy.json

# Create the IAM role
aws iam create-role \
  --role-name honua-build-orchestrator \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "ecs-tasks.amazonaws.com"},
      "Action": "sts:AssumeRole"
    }]
  }'

# Attach the policy to the role
aws iam attach-role-policy \
  --role-name honua-build-orchestrator \
  --policy-arn arn:aws:iam::ACCOUNT_ID:policy/honua-build-orchestrator-policy
```

#### Azure

```bash
# Create custom role definition
az role definition create --role-definition @azure-build-orchestrator-role.json

# Create managed identity
az identity create \
  --name honua-build-orchestrator \
  --resource-group honua-registries-production

# Assign the custom role
az role assignment create \
  --assignee $(az identity show -n honua-build-orchestrator -g honua-registries-production --query principalId -o tsv) \
  --role "Honua Build Orchestrator" \
  --scope /subscriptions/SUBSCRIPTION_ID/resourceGroups/honua-registries-production
```

#### GCP

```bash
# Create custom IAM role
gcloud iam roles create honuaBuildOrchestrator \
  --project=YOUR_PROJECT_ID \
  --file=gcp-build-orchestrator-role.yaml

# Create service account
gcloud iam service-accounts create honua-build-orchestrator \
  --display-name="Honua Build Orchestrator" \
  --project=YOUR_PROJECT_ID

# Bind the custom role
gcloud projects add-iam-policy-binding YOUR_PROJECT_ID \
  --member="serviceAccount:honua-build-orchestrator@YOUR_PROJECT_ID.iam.gserviceaccount.com" \
  --role="projects/YOUR_PROJECT_ID/roles/honuaBuildOrchestrator"
```

## Customer Provisioning Flow

When onboarding a new customer, the build orchestrator:

### AWS Flow

1. **Create ECR Repository**
   ```
   Repository: honua/customer-123
   Tags: ManagedBy=honua-orchestrator, Customer=customer-123
   ```

2. **Create Customer IAM Policy**
   ```
   Policy Name: honua-customer-customer-123-policy
   Resource: arn:aws:ecr:us-east-1:ACCOUNT:repository/honua/customer-123
   Tags: ManagedBy=honua-orchestrator, Customer=customer-123
   ```

3. **Create Customer IAM User**
   ```
   User Name: honua-customer-customer-123
   Tags: ManagedBy=honua-orchestrator, Customer=customer-123
   ```

4. **Attach Policy to User**

5. **Generate Access Keys**
   ```
   Store in customer's secret management system
   ```

### Azure Flow

1. **Create ACR Repository** (within shared registry)
   ```
   Registry: honuaregistry
   Repository: customer-123
   ```

2. **Create Scope Map**
   ```
   Name: customer-123-scope
   Actions: content/read, content/write
   Repositories: customer-123
   ```

3. **Create Token**
   ```
   Name: customer-123-token
   Scope Map: customer-123-scope
   ```

4. **Return Token Credentials**
   ```
   Store in customer's secret management system
   ```

### GCP Flow

1. **Create Artifact Registry Repository**
   ```
   Repository: customer-123
   Location: us-central1
   Format: Docker
   Labels: managed-by=honua-orchestrator, customer=customer-123
   ```

2. **Create Service Account**
   ```
   Name: honua-customer-customer-123
   Display Name: Customer 123 Registry Access
   ```

3. **Grant Repository Access**
   ```
   Role: roles/artifactregistry.reader
   Role: roles/artifactregistry.writer
   Resource: projects/PROJECT/locations/us-central1/repositories/customer-123
   ```

4. **Generate Service Account Key**
   ```
   Store in customer's secret management system
   ```

## Testing

### Test Orchestrator Permissions

#### AWS
```bash
# Assume the orchestrator role
aws sts assume-role \
  --role-arn arn:aws:iam::ACCOUNT_ID:role/honua-build-orchestrator \
  --role-session-name test

# Test ECR repository creation
aws ecr create-repository --repository-name honua/test-customer

# Test IAM user creation
aws iam create-user --user-name honua-customer-test --tags Key=ManagedBy,Value=honua-orchestrator

# Clean up
aws ecr delete-repository --repository-name honua/test-customer --force
aws iam delete-user --user-name honua-customer-test
```

#### Azure
```bash
# Login as managed identity
az login --identity

# Test ACR creation
az acr create --name testcustomeracr --resource-group honua-registries-production --sku Basic

# Test token creation
az acr scope-map create -r testcustomeracr --name test-scope --repository test-repo content/read content/write

# Clean up
az acr delete --name testcustomeracr --yes
```

#### GCP
```bash
# Authenticate as service account
gcloud auth activate-service-account --key-file=sa-key.json

# Test repository creation
gcloud artifacts repositories create test-customer \
  --repository-format=docker \
  --location=us-central1

# Test service account creation
gcloud iam service-accounts create honua-customer-test

# Clean up
gcloud artifacts repositories delete test-customer --location=us-central1 --quiet
gcloud iam service-accounts delete honua-customer-test@PROJECT.iam.gserviceaccount.com --quiet
```

### Test Customer Access

Use the generated customer credentials to verify scoped access:

```bash
# AWS - Customer should only access their repository
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin ACCOUNT.dkr.ecr.us-east-1.amazonaws.com
docker pull ACCOUNT.dkr.ecr.us-east-1.amazonaws.com/honua/customer-123:latest

# Azure - Customer token should only access their repository
docker login honuaregistry.azurecr.io --username customer-123-token --password TOKEN_PASSWORD
docker pull honuaregistry.azurecr.io/customer-123:latest

# GCP - Customer service account should only access their repository
gcloud auth activate-service-account --key-file=customer-sa-key.json
docker pull us-central1-docker.pkg.dev/PROJECT/customer-123/image:latest
```

## Monitoring and Auditing

### AWS CloudTrail

Monitor orchestrator activity:

```bash
aws cloudtrail lookup-events \
  --lookup-attributes AttributeKey=User,AttributeValue=honua-build-orchestrator \
  --max-items 50
```

Key events to monitor:
- `CreateRepository`
- `DeleteRepository`
- `CreateUser`
- `DeleteUser`
- `CreateAccessKey`
- `GetSecretValue`

### Azure Activity Log

Monitor managed identity activity:

```bash
az monitor activity-log list \
  --resource-group honua-registries-production \
  --max-events 50 \
  --query "[?caller=='honua-build-orchestrator']"
```

### GCP Cloud Audit Logs

Monitor service account activity:

```bash
gcloud logging read "protoPayload.authenticationInfo.principalEmail=honua-build-orchestrator@PROJECT.iam.gserviceaccount.com" \
  --limit 50 \
  --format json
```

## Maintenance

### Quarterly Review Checklist

- [ ] Review IAM policies for least privilege
- [ ] Audit customer IAM users/service accounts
- [ ] Remove unused customer resources
- [ ] Rotate GitHub PAT and app private keys
- [ ] Review CloudTrail/Activity Logs for anomalies
- [ ] Update regional restrictions if needed
- [ ] Verify tag enforcement on all resources

### Secret Rotation

Rotate secrets every 90 days:

```bash
# AWS - Rotate GitHub PAT
aws secretsmanager rotate-secret --secret-id honua/github-pat-build-orchestrator

# Azure - Update Key Vault secret
az keyvault secret set --vault-name honua-orchestrator-kv --name github-pat --value NEW_PAT

# GCP - Add new secret version
echo -n "NEW_PAT" | gcloud secrets versions add honua-github-pat-build-orchestrator --data-file=-
```

## Troubleshooting

See the [IAM Setup Guide](/home/mike/projects/HonuaIO/docs/IAM_SETUP.md#troubleshooting) for detailed troubleshooting steps.

## References

- [AWS IAM Policy Reference](https://docs.aws.amazon.com/IAM/latest/UserGuide/reference_policies.html)
- [Azure RBAC Custom Roles](https://docs.microsoft.com/en-us/azure/role-based-access-control/custom-roles)
- [GCP Custom Roles](https://cloud.google.com/iam/docs/creating-custom-roles)
- [ECR Policy Examples](https://docs.aws.amazon.com/AmazonECR/latest/userguide/security_iam_id-based-policy-examples.html)
- [ACR Authentication](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-authentication)
- [Artifact Registry IAM](https://cloud.google.com/artifact-registry/docs/access-control)
