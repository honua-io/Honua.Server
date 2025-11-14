# Honua IO Cloud Marketplace Deployments

This directory contains deployment templates, scripts, and documentation for launching Honua IO on cloud marketplaces.

## Supported Marketplaces

- **AWS Marketplace** - CloudFormation templates, EKS deployment, metering integration
- **Azure Marketplace** - ARM templates, AKS deployment, Partner Center integration
- **Google Cloud Marketplace** - Deployment Manager templates, GKE deployment, usage reporting

## Directory Structure

```
marketplace/
├── aws/                    # AWS Marketplace integration
│   ├── templates/          # CloudFormation templates
│   ├── scripts/            # Deployment and metering scripts
│   └── docs/               # AWS-specific documentation
├── azure/                  # Azure Marketplace integration
│   ├── templates/          # ARM templates
│   ├── scripts/            # Deployment and metering scripts
│   └── docs/               # Azure-specific documentation
└── gcp/                    # Google Cloud Marketplace integration
    ├── templates/          # Deployment Manager templates
    ├── scripts/            # Deployment and usage reporting scripts
    └── docs/               # GCP-specific documentation
```

## Quick Start

### AWS Marketplace

1. Review [AWS Marketplace Documentation](./aws/docs/README.md)
2. Deploy using CloudFormation:
   ```bash
   aws cloudformation create-stack \
     --stack-name honua-server \
     --template-body file://aws/templates/eks-deployment.yaml \
     --parameters file://aws/templates/parameters.json
   ```

### Azure Marketplace

1. Review [Azure Marketplace Documentation](./azure/docs/README.md)
2. Deploy using ARM template:
   ```bash
   az deployment group create \
     --resource-group honua-rg \
     --template-file azure/templates/aks-deployment.json \
     --parameters @azure/templates/parameters.json
   ```

### Google Cloud Marketplace

1. Review [GCP Marketplace Documentation](./gcp/docs/README.md)
2. Deploy using Deployment Manager:
   ```bash
   gcloud deployment-manager deployments create honua-server \
     --config gcp/templates/gke-deployment.yaml
   ```

## Features

### Multi-Tier Licensing
- **Free Tier** - Limited features for evaluation
- **Professional Tier** - Advanced features for small to medium deployments
- **Enterprise Tier** - Full features with unlimited scale

### Usage-Based Billing
All marketplace deployments include integrated metering for:
- API requests
- Storage usage
- Compute resources
- Data processing

### Automated Deployment
- One-click deployment from marketplace
- Automated infrastructure provisioning
- Built-in monitoring and logging
- Auto-scaling configuration

## Support

For marketplace-specific support:
- AWS: Contact through AWS Marketplace seller dashboard
- Azure: Contact through Partner Center
- GCP: Contact through GCP Marketplace support

For technical support: https://honua.io/support
