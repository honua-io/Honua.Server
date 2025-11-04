# AWS EKS Deployment Guide

## Prerequisites

1. **AWS CLI** configured with appropriate credentials
2. **kubectl** installed and configured
3. **eksctl** for EKS cluster management
4. **helm** for chart deployment

## Infrastructure Setup

### 1. Create EKS Cluster

```bash
eksctl create cluster \
  --name honua-eks-cluster \
  --region us-west-2 \
  --version 1.28 \
  --nodegroup-name honua-nodes \
  --node-type t3.xlarge \
  --nodes 3 \
  --nodes-min 3 \
  --nodes-max 10 \
  --managed \
  --with-oidc \
  --ssh-access \
  --ssh-public-key my-key-pair
```

### 2. Install AWS Load Balancer Controller

```bash
# Add the EKS chart repo
helm repo add eks https://aws.github.io/eks-charts
helm repo update

# Install AWS Load Balancer Controller
helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
  -n kube-system \
  --set clusterName=honua-eks-cluster \
  --set serviceAccount.create=true \
  --set serviceAccount.name=aws-load-balancer-controller
```

### 3. Install EBS CSI Driver

```bash
kubectl apply -k "github.com/kubernetes-sigs/aws-ebs-csi-driver/deploy/kubernetes/overlays/stable/?ref=release-1.25"
```

### 4. Install EFS CSI Driver (for shared storage)

```bash
# Create EFS filesystem
aws efs create-file-system \
  --region us-west-2 \
  --performance-mode generalPurpose \
  --throughput-mode bursting \
  --encrypted \
  --tags Key=Name,Value=honua-efs

# Install EFS CSI driver
kubectl apply -k "github.com/kubernetes-sigs/aws-efs-csi-driver/deploy/kubernetes/overlays/stable/?ref=release-1.5"
```

### 5. Create IAM Roles for Service Accounts

```bash
# Create IAM policy for API service
aws iam create-policy \
  --policy-name HonuaAPIPolicy \
  --policy-document file://iam-policy-api.json

# Create IAM role with OIDC provider
eksctl create iamserviceaccount \
  --name honua-api-sa \
  --namespace honua \
  --cluster honua-eks-cluster \
  --attach-policy-arn arn:aws:iam::ACCOUNT_ID:policy/HonuaAPIPolicy \
  --approve \
  --override-existing-serviceaccounts

# Repeat for intake and orchestrator services
eksctl create iamserviceaccount \
  --name honua-intake-sa \
  --namespace honua \
  --cluster honua-eks-cluster \
  --attach-policy-arn arn:aws:iam::ACCOUNT_ID:policy/HonuaIntakePolicy \
  --approve

eksctl create iamserviceaccount \
  --name honua-orchestrator-sa \
  --namespace honua \
  --cluster honua-eks-cluster \
  --attach-policy-arn arn:aws:iam::ACCOUNT_ID:policy/HonuaOrchestratorPolicy \
  --approve
```

## Deployment

### 1. Configure kubectl context

```bash
aws eks update-kubeconfig --region us-west-2 --name honua-eks-cluster
```

### 2. Deploy using Helm

```bash
# Create namespace
kubectl create namespace honua

# Deploy with production values
helm install honua ./deployment/helm/honua \
  -n honua \
  -f ./deployment/helm/honua/values-prod.yaml \
  --set cloudProvider.type=aws \
  --set cloudProvider.aws.region=us-west-2 \
  --set cloudProvider.aws.loadBalancer.type=nlb \
  --set cloudProvider.aws.storage.class=gp3
```

### 3. Apply AWS-specific configurations

```bash
kubectl apply -f deployment/cloud/aws/eks-config.yaml
```

## Monitoring and Observability

### Install CloudWatch Container Insights

```bash
# Install CloudWatch agent
kubectl apply -f https://raw.githubusercontent.com/aws-samples/amazon-cloudwatch-container-insights/latest/k8s-deployment-manifest-templates/deployment-mode/daemonset/container-insights-monitoring/quickstart/cwagent-fluentd-quickstart.yaml
```

## Security Best Practices

1. **Enable Pod Security Standards**
2. **Use AWS Secrets Manager** for sensitive data
3. **Enable VPC Flow Logs**
4. **Configure Security Groups** appropriately
5. **Enable AWS GuardDuty** for threat detection
6. **Use AWS KMS** for encryption at rest

## Backup and Disaster Recovery

### Setup EBS Snapshots

```bash
# Install Velero for backup
helm repo add vmware-tanzu https://vmware-tanzu.github.io/helm-charts
helm install velero vmware-tanzu/velero \
  --namespace velero \
  --create-namespace \
  --set-file credentials.secretContents.cloud=./credentials-velero \
  --set configuration.provider=aws \
  --set configuration.backupStorageLocation.bucket=honua-backups \
  --set configuration.backupStorageLocation.config.region=us-west-2 \
  --set snapshotsEnabled=true \
  --set deployRestic=true
```

## Scaling

### Configure Cluster Autoscaler

```bash
kubectl apply -f deployment/cloud/aws/cluster-autoscaler.yaml
```

## Cost Optimization

1. Use **Spot Instances** for non-critical workloads
2. Enable **EBS volume auto-deletion**
3. Use **S3 lifecycle policies** for logs
4. Configure **resource quotas** and **limits**
5. Use **Savings Plans** or **Reserved Instances**

## Troubleshooting

### Common Issues

1. **Load Balancer not provisioning**: Check AWS Load Balancer Controller logs
2. **Pods not scheduling**: Check node capacity and taints
3. **EBS volumes not attaching**: Verify EBS CSI driver installation
4. **IRSA not working**: Verify OIDC provider configuration

### Useful Commands

```bash
# Check AWS Load Balancer Controller
kubectl logs -n kube-system deployment/aws-load-balancer-controller

# Check EBS CSI driver
kubectl logs -n kube-system daemonset/ebs-csi-node

# Describe load balancer
aws elbv2 describe-load-balancers --region us-west-2

# Check IAM role for service account
kubectl describe sa honua-api-sa -n honua
```
