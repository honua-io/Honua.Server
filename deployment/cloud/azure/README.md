# Azure AKS Deployment Guide

## Prerequisites

1. **Azure CLI** installed and authenticated
2. **kubectl** installed
3. **helm** installed
4. Active Azure subscription

## Infrastructure Setup

### 1. Create Resource Group

```bash
az group create \
  --name honua-rg \
  --location westus2
```

### 2. Create AKS Cluster

```bash
az aks create \
  --resource-group honua-rg \
  --name honua-aks-cluster \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --enable-cluster-autoscaler \
  --min-count 3 \
  --max-count 10 \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --network-plugin azure \
  --enable-addons monitoring \
  --generate-ssh-keys \
  --kubernetes-version 1.28.0
```

### 3. Get AKS Credentials

```bash
az aks get-credentials \
  --resource-group honua-rg \
  --name honua-aks-cluster
```

### 4. Install Application Gateway Ingress Controller (Optional)

```bash
# Create Application Gateway
az network application-gateway create \
  --name honua-appgw \
  --resource-group honua-rg \
  --location westus2 \
  --sku Standard_v2 \
  --capacity 2 \
  --vnet-name aks-vnet \
  --subnet appgw-subnet

# Install AGIC using Helm
helm repo add application-gateway-kubernetes-ingress https://appgwingress.blob.core.windows.net/ingress-azure-helm-package/
helm repo update

helm install ingress-azure \
  application-gateway-kubernetes-ingress/ingress-azure \
  --namespace kube-system \
  --set appgw.subscriptionId=<SUBSCRIPTION_ID> \
  --set appgw.resourceGroup=honua-rg \
  --set appgw.name=honua-appgw \
  --set appgw.shared=false \
  --set armAuth.type=workloadIdentity \
  --set armAuth.identityClientID=<IDENTITY_CLIENT_ID>
```

### 5. Create Azure Storage Account for Shared Storage

```bash
# Create storage account
az storage account create \
  --name honuafilestorage \
  --resource-group honua-rg \
  --location westus2 \
  --sku Premium_LRS \
  --kind FileStorage \
  --enable-large-file-share

# Get storage account key
STORAGE_KEY=$(az storage account keys list \
  --resource-group honua-rg \
  --account-name honuafilestorage \
  --query '[0].value' -o tsv)

# Create Kubernetes secret for Azure Files
kubectl create secret generic azure-files-secret \
  -n honua \
  --from-literal=azurestorageaccountname=honuafilestorage \
  --from-literal=azurestorageaccountkey=$STORAGE_KEY
```

### 6. Setup Workload Identity

```bash
# Create managed identity for API service
az identity create \
  --name honua-api-identity \
  --resource-group honua-rg

# Get identity client ID
API_CLIENT_ID=$(az identity show \
  --name honua-api-identity \
  --resource-group honua-rg \
  --query clientId -o tsv)

# Federate identity with AKS OIDC issuer
OIDC_ISSUER=$(az aks show \
  --name honua-aks-cluster \
  --resource-group honua-rg \
  --query oidcIssuerProfile.issuerUrl -o tsv)

az identity federated-credential create \
  --name honua-api-federated-credential \
  --identity-name honua-api-identity \
  --resource-group honua-rg \
  --issuer $OIDC_ISSUER \
  --subject system:serviceaccount:honua:honua-api-sa

# Repeat for intake and orchestrator services
```

### 7. Enable Azure Monitor Container Insights

```bash
az aks enable-addons \
  --resource-group honua-rg \
  --name honua-aks-cluster \
  --addons monitoring \
  --workspace-resource-id /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/honua-rg/providers/Microsoft.OperationalInsights/workspaces/honua-logs
```

## Deployment

### 1. Deploy using Helm

```bash
# Create namespace
kubectl create namespace honua

# Deploy with production values
helm install honua ./deployment/helm/honua \
  -n honua \
  -f ./deployment/helm/honua/values-prod.yaml \
  --set cloudProvider.type=azure \
  --set cloudProvider.azure.region=westus2 \
  --set cloudProvider.azure.storage.class=managed-premium
```

### 2. Apply Azure-specific configurations

```bash
kubectl apply -f deployment/cloud/azure/aks-config.yaml
```

## Security

### 1. Enable Azure Policy for AKS

```bash
az aks enable-addons \
  --resource-group honua-rg \
  --name honua-aks-cluster \
  --addons azure-policy
```

### 2. Configure Network Policies

```bash
# Network policies are automatically supported in AKS with Azure CNI
kubectl apply -f deployment/k8s/base/networkpolicy.yaml
```

### 3. Integrate with Azure Key Vault

```bash
# Install Azure Key Vault Provider for Secrets Store CSI Driver
helm repo add csi-secrets-store-provider-azure https://azure.github.io/secrets-store-csi-driver-provider-azure/charts
helm install csi-secrets-store-provider-azure/csi-secrets-store-provider-azure \
  --namespace kube-system \
  --generate-name
```

## Monitoring

### Access Azure Monitor Metrics

```bash
# View in Azure Portal
az aks browse \
  --resource-group honua-rg \
  --name honua-aks-cluster
```

### Configure Log Analytics Queries

```kusto
ContainerLog
| where Namespace == "honua"
| where ContainerName startswith "honua"
| project TimeGenerated, ContainerName, LogEntry
| order by TimeGenerated desc
```

## Backup and Disaster Recovery

### Setup Azure Backup for AKS

```bash
# Enable backup
az backup vault create \
  --resource-group honua-rg \
  --name honua-backup-vault \
  --location westus2

# Configure backup policy
az backup policy create \
  --resource-group honua-rg \
  --vault-name honua-backup-vault \
  --name honua-daily-backup \
  --backup-management-type AzureIaasVM \
  --policy honua-backup-policy.json
```

## Auto-scaling

### Configure Cluster Autoscaler

```bash
# Already enabled during cluster creation
# Adjust if needed
az aks update \
  --resource-group honua-rg \
  --name honua-aks-cluster \
  --enable-cluster-autoscaler \
  --min-count 3 \
  --max-count 20
```

### Configure KEDA for event-driven scaling

```bash
helm repo add kedacore https://kedacore.github.io/charts
helm install keda kedacore/keda --namespace keda --create-namespace
```

## Cost Optimization

1. Use **Azure Reserved Instances** for predictable workloads
2. Configure **auto-shutdown** for non-production clusters
3. Use **Spot VM node pools** for batch workloads
4. Enable **Azure Advisor** recommendations
5. Monitor costs with **Azure Cost Management**

### Create Spot VM Node Pool

```bash
az aks nodepool add \
  --resource-group honua-rg \
  --cluster-name honua-aks-cluster \
  --name spotnodepool \
  --priority Spot \
  --eviction-policy Delete \
  --spot-max-price -1 \
  --enable-cluster-autoscaler \
  --min-count 0 \
  --max-count 5 \
  --node-count 1 \
  --node-vm-size Standard_D4s_v3 \
  --labels workload=batch
```

## Troubleshooting

### Common Issues

1. **Pod stuck in ContainerCreating**: Check Azure Disk attachment
2. **Load balancer not provisioning**: Verify service annotations
3. **Workload identity not working**: Check federated credentials
4. **Network policy blocking traffic**: Review NSG rules

### Useful Commands

```bash
# Check cluster health
az aks show \
  --resource-group honua-rg \
  --name honua-aks-cluster \
  --query provisioningState

# View node resource usage
kubectl top nodes

# Check Azure Monitor logs
az monitor log-analytics query \
  --workspace /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/honua-rg/providers/Microsoft.OperationalInsights/workspaces/honua-logs \
  --analytics-query "ContainerLog | where Namespace == 'honua' | limit 100"

# Restart deployment
kubectl rollout restart deployment/honua-api -n honua
```

## Support Resources

- [AKS Documentation](https://docs.microsoft.com/en-us/azure/aks/)
- [Azure Monitor for Containers](https://docs.microsoft.com/en-us/azure/azure-monitor/containers/container-insights-overview)
- [Azure Workload Identity](https://azure.github.io/azure-workload-identity/)
