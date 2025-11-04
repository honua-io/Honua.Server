# HonuaIO Dynamic IAM Permission Generation

The HonuaIO AI deployment agent can dynamically analyze your deployment topology and generate least-privilege IAM/RBAC policies with Terraform configuration.

## How It Works

1. **Deployment Analysis**: The AI agent analyzes your HonuaIO deployment plan, examining:
   - Database requirements (PostgreSQL configuration)
   - Compute resources (VMs, containers, scaling)
   - Storage backends (S3, Azure Blob, GCS)
   - Networking (load balancers, VPNs, public access)
   - Monitoring and logging requirements
   - Enabled OGC services and features

2. **Permission Inference**: Based on the topology, the agent determines exactly which cloud services and permissions are needed:
   - AWS: IAM policies for EC2, RDS, S3, ELB, CloudWatch, Secrets Manager
   - Azure: Custom RBAC roles for VMs, PostgreSQL, Storage, Networking, Key Vault
   - GCP: IAM bindings for Compute Engine, Cloud SQL, Cloud Storage, etc.

3. **Terraform Generation**: Produces production-ready Terraform configuration:
   - Service principal/user creation
   - Least-privilege IAM policies
   - Role assignments scoped to specific resources
   - Security best practices (MFA, conditions, resource restrictions)
   - Usage instructions and credential outputs

## Usage

### Via CLI Command

```bash
# Generate IAM permissions for AWS deployment
honua deploy generate-iam \
  --cloud aws \
  --region us-east-1 \
  --environment prod \
  --config config/honua-config.json

# Generate RBAC permissions for Azure deployment
honua deploy generate-iam \
  --cloud azure \
  --region eastus \
  --environment prod \
  --config config/honua-config.json

# Use with deployment plan
honua deploy plan --output plan.json
honua deploy generate-iam \
  --cloud aws \
  --region us-west-2 \
  --environment staging \
  --from-plan plan.json
```

### Via AI Agent Conversation

```bash
honua ai

> I want to deploy HonuaIO to AWS us-east-1 for production with PostgreSQL,
> S3 storage, and load balancing. Generate the IAM permissions I need.

AI Agent: I'll analyze that deployment topology and generate least-privilege
IAM permissions for you...

[Agent generates permissions and Terraform configuration]

> Apply the generated IAM configuration

AI Agent: Running terraform init and apply for IAM user creation...
```

### Manual Generation

1. **Create deployment topology** (JSON or YAML):

```json
{
  "cloudProvider": "aws",
  "region": "us-east-1",
  "environment": "prod",
  "database": {
    "engine": "postgres",
    "version": "15",
    "instanceSize": "db.r6g.xlarge",
    "storageGB": 100,
    "highAvailability": true
  },
  "compute": {
    "type": "container",
    "instanceSize": "c6i.2xlarge",
    "instanceCount": 3,
    "autoScaling": true
  },
  "storage": {
    "type": "s3",
    "attachmentStorageGB": 100,
    "rasterCacheGB": 500,
    "replication": "cross-region"
  },
  "networking": {
    "loadBalancer": true,
    "publicAccess": true,
    "vpnRequired": false
  },
  "monitoring": {
    "provider": "cloudwatch",
    "enableMetrics": true,
    "enableLogs": true,
    "enableTracing": true
  },
  "features": [
    "OGC WFS 2.0",
    "OGC WMS 1.3",
    "OGC WMTS 1.0",
    "OData v4",
    "Vector Tiles",
    "Raster Tiles",
    "STAC Catalog"
  ]
}
```

2. **Run generation**:

```bash
honua deploy generate-iam --topology topology.json --output ./iam
```

3. **Apply Terraform configuration**:

```bash
cd ./iam
terraform init
terraform plan
terraform apply

# Save credentials securely
terraform output -json > credentials.json.enc
# Encrypt with age, gpg, or store in password manager
```

## Generated Files

### AWS

- `iam-deployer.tf` - IAM user and policies
- `variables.tf` - Input variables
- `outputs.tf` - Credentials and usage instructions
- `README.md` - Deployment-specific documentation

### Azure

- `rbac-deployer.tf` - Service principal and custom roles
- `variables.tf` - Input variables
- `outputs.tf` - Credentials and usage instructions
- `README.md` - Deployment-specific documentation

### GCP

- `iam-deployer.tf` - Service account and IAM bindings
- `variables.tf` - Input variables
- `outputs.tf` - Credentials and usage instructions
- `README.md` - Deployment-specific documentation

## Security Best Practices

### Principle of Least Privilege

The generated permissions follow least-privilege principles:

1. **Resource-level restrictions**: Policies scoped to specific resource ARNs/IDs
2. **Conditional access**: IP restrictions, MFA requirements where appropriate
3. **Read-only separation**: Separate policies for read vs write operations
4. **Time-based limits**: Service principal passwords expire in 90 days (configurable)
5. **Action minimization**: Only permissions actually needed for deployment

### Credential Management

**DO**:
- Store credentials in secure password managers (1Password, LastPass, Azure Key Vault)
- Rotate access keys/passwords every 90 days
- Use temporary credentials (AWS STS, Azure Managed Identity) when possible
- Enable MFA for production service principals
- Monitor credential usage in cloud provider audit logs

**DON'T**:
- Commit credentials to version control
- Share credentials via email or chat
- Reuse credentials across environments
- Use long-lived credentials for automated deployments (prefer service roles)

### Validation

The agent validates generated permissions:

1. **Syntax validation**: Ensures IAM JSON is valid
2. **Resource validation**: Checks ARN/ID formats
3. **Scope validation**: Ensures permissions don't exceed requirements
4. **Best practice checks**: Flags overly permissive policies (wildcards, etc.)

## Examples

### Minimal Development Deployment

```json
{
  "cloudProvider": "aws",
  "region": "us-east-2",
  "environment": "dev",
  "database": {
    "engine": "postgres",
    "version": "15",
    "instanceSize": "db.t4g.micro",
    "storageGB": 20,
    "highAvailability": false
  },
  "compute": {
    "type": "container",
    "instanceSize": "t3.medium",
    "instanceCount": 1,
    "autoScaling": false
  },
  "storage": {
    "type": "s3",
    "attachmentStorageGB": 10,
    "rasterCacheGB": 50,
    "replication": "single-region"
  }
}
```

**Generated permissions**: ~8 IAM policies (EC2, RDS, S3, CloudWatch basics)

### Production High-Availability Deployment

```json
{
  "cloudProvider": "azure",
  "region": "eastus",
  "environment": "prod",
  "database": {
    "engine": "postgres",
    "version": "15",
    "instanceSize": "GP_Gen5_8",
    "storageGB": 500,
    "highAvailability": true
  },
  "compute": {
    "type": "container",
    "instanceSize": "Standard_D4s_v5",
    "instanceCount": 5,
    "autoScaling": true
  },
  "storage": {
    "type": "blob",
    "attachmentStorageGB": 1000,
    "rasterCacheGB": 5000,
    "replication": "geo-redundant"
  },
  "networking": {
    "loadBalancer": true,
    "publicAccess": true,
    "vpnRequired": true
  },
  "monitoring": {
    "provider": "application-insights",
    "enableMetrics": true,
    "enableLogs": true,
    "enableTracing": true
  }
}
```

**Generated permissions**: Custom RBAC role + Network Contributor + Reader

## Customization

### Modifying Generated Permissions

The generated Terraform is fully editable:

1. **Add conditions**: Include IP restrictions, MFA requirements
2. **Scope narrowing**: Further restrict to specific resources
3. **Add monitoring**: Include CloudTrail, Azure Activity Log alerts
4. **Compliance tags**: Add organization-specific tags/labels

### Template Override

Override default templates:

```bash
honua deploy generate-iam \
  --template ./custom-iam-template.tf \
  --topology topology.json
```

### LLM Prompt Tuning

Adjust AI generation behavior:

```bash
# More restrictive
honua deploy generate-iam --strictness high --topology topology.json

# Include organization policies
honua deploy generate-iam \
  --org-policy ./org-policy.json \
  --topology topology.json
```

## Troubleshooting

### "Generated permissions too broad"

The AI agent aims for minimal permissions, but may include broader policies if:
- Deployment topology is complex
- Cloud provider doesn't support fine-grained permissions for that service

**Solution**: Manually edit generated Terraform to add resource-level restrictions

### "Terraform apply fails with permissions error"

The deploying principal (your user) needs permission to create IAM resources:

**AWS**: `iam:CreateUser`, `iam:CreatePolicy`, `iam:AttachUserPolicy`
**Azure**: `Microsoft.Authorization/roleDefinitions/write`, `Microsoft.Authorization/roleAssignments/write`

**Solution**: Run as account administrator or request permissions from your cloud admin

### "Service principal can't deploy resources"

After applying IAM configuration, test the new principal:

```bash
# AWS
aws sts get-caller-identity --profile honua-deployer

# Azure
az login --service-principal --username $CLIENT_ID --password $CLIENT_SECRET --tenant $TENANT_ID
az account show
```

## Support

For issues with IAM generation:
- Check AI agent logs: `~/.honua/logs/ai-agent.log`
- Validate topology: `honua deploy validate-topology topology.json`
- Report bugs: https://github.com/your-org/honuaio/issues
