# AWS Marketplace Deployment Guide

This guide covers deploying Honua IO Server from AWS Marketplace.

## Overview

Honua IO Server is available on AWS Marketplace as a container-based product that deploys on Amazon EKS (Elastic Kubernetes Service). The deployment includes:

- **EKS Cluster** - Managed Kubernetes cluster
- **RDS PostgreSQL** - Managed database with Multi-AZ
- **ElastiCache Redis** - Managed caching layer
- **S3 Bucket** - Object storage for attachments and data
- **Integrated Metering** - Automatic usage tracking and billing

## Prerequisites

1. **AWS Account** with appropriate permissions
2. **AWS CLI** installed and configured
3. **kubectl** installed (v1.29 or later)
4. **Sufficient Service Quotas**:
   - EKS clusters: 1+
   - VPCs: 1+
   - NAT Gateways: 2+
   - Elastic IPs: 2+
   - RDS instances: 1+
   - ElastiCache clusters: 1+

## Deployment Steps

### 1. Subscribe to Honua IO on AWS Marketplace

1. Navigate to [AWS Marketplace](https://aws.amazon.com/marketplace)
2. Search for "Honua IO Server"
3. Click "Continue to Subscribe"
4. Accept the terms and conditions
5. Click "Continue to Configuration"

### 2. Deploy via CloudFormation

#### Option A: AWS Marketplace Console (Recommended)

1. On the configuration page, select:
   - **Delivery Method**: CloudFormation Template
   - **Software Version**: Latest
   - **Region**: Your preferred region
2. Click "Continue to Launch"
3. Select "Launch CloudFormation"
4. Fill in the parameters:
   - **Cluster Name**: Name for your EKS cluster
   - **Node Instance Type**: EC2 instance type (default: t3.large)
   - **Node Group Size**: Min/Max/Desired nodes
   - **License Tier**: Free/Professional/Enterprise
   - **Database Settings**: RDS instance class and storage
   - **Redis Settings**: ElastiCache node type
5. Review and create the stack

#### Option B: AWS CLI

```bash
# Download the CloudFormation template
aws s3 cp s3://aws-marketplace-templates/honua-server/eks-deployment.yaml .

# Create the stack
aws cloudformation create-stack \
  --stack-name honua-server \
  --template-body file://eks-deployment.yaml \
  --parameters \
    ParameterKey=ClusterName,ParameterValue=honua-server \
    ParameterKey=LicenseTier,ParameterValue=Professional \
    ParameterKey=NodeInstanceType,ParameterValue=t3.large \
    ParameterKey=NodeGroupDesiredSize,ParameterValue=2 \
  --capabilities CAPABILITY_IAM \
  --region us-east-1

# Wait for stack creation
aws cloudformation wait stack-create-complete \
  --stack-name honua-server \
  --region us-east-1
```

### 3. Configure kubectl

```bash
# Get cluster name from CloudFormation outputs
CLUSTER_NAME=$(aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`ClusterName`].OutputValue' \
  --output text)

# Update kubeconfig
aws eks update-kubeconfig \
  --name $CLUSTER_NAME \
  --region us-east-1

# Verify connection
kubectl get nodes
```

### 4. Deploy Honua Server Application

```bash
# Get CloudFormation outputs
DB_ENDPOINT=$(aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`DatabaseEndpoint`].OutputValue' \
  --output text)

REDIS_ENDPOINT=$(aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`RedisEndpoint`].OutputValue' \
  --output text)

S3_BUCKET=$(aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`S3BucketName`].OutputValue' \
  --output text)

SERVICE_ACCOUNT_ROLE=$(aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`ServiceAccountRoleArn`].OutputValue' \
  --output text)

DB_PASSWORD_SECRET=$(aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`DatabasePasswordSecretArn`].OutputValue' \
  --output text)

# Get database password
DB_PASSWORD=$(aws secretsmanager get-secret-value \
  --secret-id $DB_PASSWORD_SECRET \
  --query 'SecretString' \
  --output text | jq -r '.password')

# Apply Kubernetes manifests with substitutions
export DATABASE_ENDPOINT=$DB_ENDPOINT
export DATABASE_PORT=5432
export REDIS_ENDPOINT=$REDIS_ENDPOINT
export REDIS_PORT=6379
export S3_BUCKET_NAME=$S3_BUCKET
export SERVICE_ACCOUNT_ROLE_ARN=$SERVICE_ACCOUNT_ROLE
export DATABASE_PASSWORD=$DB_PASSWORD
export AWS_REGION=us-east-1
export MARKETPLACE_PRODUCT_CODE="your-product-code"
export LICENSE_TIER="Professional"
export IMAGE_TAG="latest"

# Download and apply manifests
curl -O https://raw.githubusercontent.com/honua-io/Honua.Server/main/deploy/marketplace/aws/templates/kubernetes-manifest.yaml

envsubst < kubernetes-manifest.yaml | kubectl apply -f -
```

### 5. Access Honua Server

```bash
# Get LoadBalancer URL
kubectl get service honua-server -n honua-system

# Wait for LoadBalancer to be provisioned
kubectl wait --for=condition=available --timeout=300s \
  deployment/honua-server -n honua-system

# Get the external URL
HONUA_URL=$(kubectl get service honua-server -n honua-system \
  -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')

echo "Honua Server is available at: http://$HONUA_URL"
```

### 6. Initial Configuration

```bash
# Access the web interface
open "http://$HONUA_URL"

# Create initial admin user (if not using SSO)
# Follow the on-screen setup wizard
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         AWS Account                          │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │                    VPC (10.0.0.0/16)                │    │
│  │                                                     │    │
│  │  ┌──────────────┐        ┌──────────────┐         │    │
│  │  │ Public       │        │ Public       │         │    │
│  │  │ Subnet 1     │        │ Subnet 2     │         │    │
│  │  │ (NAT GW)     │        │ (NAT GW)     │         │    │
│  │  └──────────────┘        └──────────────┘         │    │
│  │                                                     │    │
│  │  ┌──────────────┐        ┌──────────────┐         │    │
│  │  │ Private      │        │ Private      │         │    │
│  │  │ Subnet 1     │        │ Subnet 2     │         │    │
│  │  │              │        │              │         │    │
│  │  │ ┌──────────┐ │        │ ┌──────────┐ │         │    │
│  │  │ │EKS Nodes │ │        │ │EKS Nodes │ │         │    │
│  │  │ │          │ │        │ │          │ │         │    │
│  │  │ │ Honua    │ │        │ │ Honua    │ │         │    │
│  │  │ │ Server   │ │        │ │ Server   │ │         │    │
│  │  │ │ Pods     │ │        │ │ Pods     │ │         │    │
│  │  │ └──────────┘ │        │ └──────────┘ │         │    │
│  │  │              │        │              │         │    │
│  │  │ ┌──────────┐ │        │ ┌──────────┐ │         │    │
│  │  │ │RDS       │ │        │ │RDS       │ │         │    │
│  │  │ │PostgreSQL│ │        │ │Standby   │ │         │    │
│  │  │ └──────────┘ │        │ └──────────┘ │         │    │
│  │  │              │        │              │         │    │
│  │  │ ┌──────────┐ │        │ ┌──────────┐ │         │    │
│  │  │ │ElastiCache│        │ │ElastiCache│         │    │
│  │  │ │Redis     │ │        │ │Redis     │ │         │    │
│  │  │ │Primary   │ │        │ │Replica   │ │         │    │
│  │  │ └──────────┘ │        │ └──────────┘ │         │    │
│  │  └──────────────┘        └──────────────┘         │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │                   S3 Bucket                         │    │
│  │            (Attachments & Data)                     │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │              Secrets Manager                        │    │
│  │          (Database Credentials)                     │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Pricing and Metering

### Billing Model

Honua IO Server uses AWS Marketplace Metering Service for usage-based billing:

- **API Requests**: Billed per 1,000 requests
- **Storage**: Billed per GB-month
- **Data Processing**: Billed per GB processed
- **Compute**: Based on instance hours

### Metering Dimensions

| Dimension | Unit | Description |
|-----------|------|-------------|
| `api-requests` | 1,000 requests | API calls to Honua Server |
| `storage` | GB-month | Data stored in S3 and database |
| `data-processing` | GB | Raster/vector processing volume |
| `users` | Users | Active users (Professional tier) |
| `instances` | Instance-hours | Running container instances |

### Cost Optimization

1. **Use Auto-scaling**: HPA adjusts replicas based on load
2. **Right-size instances**: Start with t3.large, scale up if needed
3. **Monitor usage**: Use CloudWatch dashboards
4. **Enable S3 lifecycle policies**: Archive old data
5. **Use Reserved Instances**: For predictable workloads

## Monitoring and Logging

### CloudWatch Integration

```bash
# View application logs
aws logs tail /aws/eks/$CLUSTER_NAME/cluster --follow

# View metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/EKS \
  --metric-name cluster_failed_node_count \
  --dimensions Name=ClusterName,Value=$CLUSTER_NAME \
  --statistics Average \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300
```

### Prometheus and Grafana (Optional)

```bash
# Deploy Prometheus
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace

# Access Grafana dashboard
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80
```

## Troubleshooting

### Common Issues

#### 1. Pods not starting

```bash
# Check pod status
kubectl get pods -n honua-system

# View pod logs
kubectl logs -n honua-system deployment/honua-server

# Describe pod for events
kubectl describe pod -n honua-system <pod-name>
```

#### 2. Database connection issues

```bash
# Test database connectivity
kubectl run -it --rm debug --image=postgres:16 --restart=Never -- \
  psql -h $DB_ENDPOINT -U honua -d honua

# Check security groups
aws ec2 describe-security-groups \
  --filters "Name=tag:Name,Values=honua-server-rds-sg"
```

#### 3. Marketplace metering not working

```bash
# Check service account annotations
kubectl describe sa honua-server -n honua-system

# Verify IAM role permissions
aws iam get-role-policy \
  --role-name HonuaServiceAccountRole \
  --policy-name HonuaServerPolicy

# Check application logs for metering errors
kubectl logs -n honua-system deployment/honua-server | grep -i marketplace
```

#### 4. LoadBalancer not provisioning

```bash
# Check service events
kubectl describe service honua-server -n honua-system

# Verify AWS Load Balancer Controller
kubectl get deployment -n kube-system aws-load-balancer-controller
```

## Upgrading

### Update Honua Server Version

```bash
# Update image tag in deployment
kubectl set image deployment/honua-server \
  honua-server=ghcr.io/honua-io/honua-server:v1.2.0 \
  -n honua-system

# Monitor rollout
kubectl rollout status deployment/honua-server -n honua-system

# Rollback if needed
kubectl rollout undo deployment/honua-server -n honua-system
```

### Update CloudFormation Stack

```bash
# Update stack with new parameters
aws cloudformation update-stack \
  --stack-name honua-server \
  --template-body file://eks-deployment.yaml \
  --parameters \
    ParameterKey=NodeInstanceType,ParameterValue=t3.xlarge \
  --capabilities CAPABILITY_IAM
```

## Security Best Practices

1. **Enable VPC Flow Logs** for network monitoring
2. **Use AWS Secrets Manager** for all credentials
3. **Enable RDS encryption at rest**
4. **Enable S3 bucket encryption**
5. **Configure Network Policies** in Kubernetes
6. **Enable pod security standards**
7. **Regular security updates** via rolling updates
8. **Use IAM roles** instead of access keys

## Support

- **Documentation**: https://docs.honua.io
- **AWS Marketplace Support**: Contact through AWS Marketplace
- **Technical Support**: support@honua.io
- **Community**: https://community.honua.io

## Uninstalling

```bash
# Delete Kubernetes resources
kubectl delete namespace honua-system

# Delete CloudFormation stack
aws cloudformation delete-stack --stack-name honua-server

# Wait for deletion
aws cloudformation wait stack-delete-complete --stack-name honua-server
```

**Note**: S3 buckets with data may need to be emptied manually before deletion.
