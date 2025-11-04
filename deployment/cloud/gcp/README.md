# Google Cloud GKE Deployment Guide

## Prerequisites

1. **gcloud CLI** installed and authenticated
2. **kubectl** installed
3. **helm** installed
4. Active GCP project with billing enabled

## Infrastructure Setup

### 1. Set GCP Project

```bash
export PROJECT_ID="your-project-id"
export REGION="us-central1"
export ZONE="us-central1-a"

gcloud config set project $PROJECT_ID
gcloud config set compute/region $REGION
gcloud config set compute/zone $ZONE
```

### 2. Enable Required APIs

```bash
gcloud services enable \
  container.googleapis.com \
  compute.googleapis.com \
  monitoring.googleapis.com \
  logging.googleapis.com \
  cloudtrace.googleapis.com \
  clouderrorreporting.googleapis.com \
  containerregistry.googleapis.com \
  artifactregistry.googleapis.com \
  servicenetworking.googleapis.com \
  file.googleapis.com
```

### 3. Create GKE Cluster

```bash
gcloud container clusters create honua-gke-cluster \
  --region $REGION \
  --node-locations $ZONE \
  --num-nodes 3 \
  --min-nodes 3 \
  --max-nodes 10 \
  --enable-autoscaling \
  --machine-type n2-standard-4 \
  --disk-type pd-ssd \
  --disk-size 100 \
  --enable-autorepair \
  --enable-autoupgrade \
  --enable-ip-alias \
  --network "default" \
  --subnetwork "default" \
  --enable-stackdriver-kubernetes \
  --enable-cloud-logging \
  --enable-cloud-monitoring \
  --addons HorizontalPodAutoscaling,HttpLoadBalancing,GcePersistentDiskCsiDriver \
  --workload-pool=$PROJECT_ID.svc.id.goog \
  --enable-shielded-nodes \
  --shielded-secure-boot \
  --shielded-integrity-monitoring \
  --release-channel regular
```

### 4. Get Cluster Credentials

```bash
gcloud container clusters get-credentials honua-gke-cluster --region $REGION
```

### 5. Create Static IP for Load Balancer

```bash
gcloud compute addresses create honua-static-ip \
  --global \
  --ip-version IPV4

# Get the IP address
gcloud compute addresses describe honua-static-ip --global --format="value(address)"
```

### 6. Setup Workload Identity

```bash
# Create GCP service accounts
gcloud iam service-accounts create honua-api \
  --display-name="Honua API Service Account"

gcloud iam service-accounts create honua-intake \
  --display-name="Honua Intake Service Account"

gcloud iam service-accounts create honua-orchestrator \
  --display-name="Honua Orchestrator Service Account"

# Grant necessary IAM roles
gcloud projects add-iam-policy-binding $PROJECT_ID \
  --member="serviceAccount:honua-api@$PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/cloudtrace.agent"

gcloud projects add-iam-policy-binding $PROJECT_ID \
  --member="serviceAccount:honua-api@$PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/monitoring.metricWriter"

# Bind Kubernetes service accounts to GCP service accounts
kubectl create namespace honua

gcloud iam service-accounts add-iam-policy-binding \
  honua-api@$PROJECT_ID.iam.gserviceaccount.com \
  --role roles/iam.workloadIdentityUser \
  --member "serviceAccount:$PROJECT_ID.svc.id.goog[honua/honua-api-sa]"

# Repeat for other services
gcloud iam service-accounts add-iam-policy-binding \
  honua-intake@$PROJECT_ID.iam.gserviceaccount.com \
  --role roles/iam.workloadIdentityUser \
  --member "serviceAccount:$PROJECT_ID.svc.id.goog[honua/honua-intake-sa]"

gcloud iam service-accounts add-iam-policy-binding \
  honua-orchestrator@$PROJECT_ID.iam.gserviceaccount.com \
  --role roles/iam.workloadIdentityUser \
  --member "serviceAccount:$PROJECT_ID.svc.id.goog[honua/honua-orchestrator-sa]"
```

### 7. Create Filestore Instance (for shared storage)

```bash
gcloud filestore instances create honua-filestore \
  --zone=$ZONE \
  --tier=PREMIUM \
  --file-share=name="honua_share",capacity=1TB \
  --network=name="default"

# Get Filestore IP
FILESTORE_IP=$(gcloud filestore instances describe honua-filestore \
  --zone=$ZONE \
  --format="value(networks.ipAddresses[0])")
```

### 8. Create Cloud Armor Security Policy

```bash
gcloud compute security-policies create honua-security-policy \
  --description "Security policy for Honua API"

# Add rules
gcloud compute security-policies rules create 1000 \
  --security-policy honua-security-policy \
  --expression "origin.region_code == 'CN'" \
  --action "deny-403" \
  --description "Block traffic from specific regions"

gcloud compute security-policies rules create 2000 \
  --security-policy honua-security-policy \
  --expression "evaluatePreconfiguredExpr('xss-stable')" \
  --action "deny-403" \
  --description "Block XSS attacks"

gcloud compute security-policies rules create 3000 \
  --security-policy honua-security-policy \
  --expression "evaluatePreconfiguredExpr('sqli-stable')" \
  --action "deny-403" \
  --description "Block SQL injection"

# Default rule - allow
gcloud compute security-policies rules create 2147483647 \
  --security-policy honua-security-policy \
  --action "allow" \
  --description "Default rule"
```

## Deployment

### 1. Push Images to Google Container Registry

```bash
# Tag images for GCR
docker tag honua/server-host:latest gcr.io/$PROJECT_ID/honua/server-host:latest
docker tag honua/server-intake:latest gcr.io/$PROJECT_ID/honua/server-intake:latest
docker tag honua/build-orchestrator:latest gcr.io/$PROJECT_ID/honua/build-orchestrator:latest

# Configure Docker for GCR
gcloud auth configure-docker

# Push images
docker push gcr.io/$PROJECT_ID/honua/server-host:latest
docker push gcr.io/$PROJECT_ID/honua/server-intake:latest
docker push gcr.io/$PROJECT_ID/honua/build-orchestrator:latest
```

### 2. Deploy using Helm

```bash
# Deploy with production values
helm install honua ./deployment/helm/honua \
  -n honua \
  -f ./deployment/helm/honua/values-prod.yaml \
  --set cloudProvider.type=gcp \
  --set cloudProvider.gcp.region=$REGION \
  --set cloudProvider.gcp.storage.class=pd-ssd \
  --set image.registry=gcr.io/$PROJECT_ID
```

### 3. Apply GCP-specific configurations

```bash
# Update PROJECT_ID in the config file first
sed -i "s/PROJECT_ID/$PROJECT_ID/g" deployment/cloud/gcp/gke-config.yaml

# Apply configurations
kubectl apply -f deployment/cloud/gcp/gke-config.yaml
```

### 4. Configure DNS

```bash
# Get the load balancer IP
LB_IP=$(kubectl get ingress honua-gcp-ingress -n honua -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Add DNS records
# api.honua.io -> $LB_IP
# intake.honua.io -> $LB_IP
```

## Monitoring with Google Cloud Operations

### 1. View Logs

```bash
# View logs in Cloud Console or use gcloud
gcloud logging read "resource.type=k8s_container AND resource.labels.namespace_name=honua" \
  --limit 50 \
  --format json
```

### 2. Create Custom Metrics

```bash
gcloud monitoring dashboards create --config-from-file=./deployment/cloud/gcp/dashboard.json
```

### 3. Setup Alerting

```bash
# Create notification channel
gcloud alpha monitoring channels create \
  --display-name="Honua Team Email" \
  --type=email \
  --channel-labels=email_address=team@honua.io

# Create alert policy
gcloud alpha monitoring policies create \
  --notification-channels=CHANNEL_ID \
  --display-name="High API Error Rate" \
  --condition-display-name="Error rate > 5%" \
  --condition-threshold-value=0.05 \
  --condition-threshold-duration=300s
```

## Backup and Disaster Recovery

### 1. Enable Backup for GKE

```bash
gcloud container clusters update honua-gke-cluster \
  --region $REGION \
  --enable-backup-restore

# Create backup plan
gcloud container backup-restore backup-plans create honua-backup-plan \
  --cluster=projects/$PROJECT_ID/locations/$REGION/clusters/honua-gke-cluster \
  --location=$REGION \
  --all-namespaces \
  --include-secrets \
  --include-volume-data \
  --cron-schedule="0 2 * * *"
```

### 2. Setup Persistent Disk Snapshots

```bash
# Create snapshot schedule
gcloud compute resource-policies create snapshot-schedule honua-daily-snapshots \
  --region $REGION \
  --max-retention-days 7 \
  --on-source-disk-delete keep-auto-snapshots \
  --daily-schedule \
  --start-time 02:00
```

## Auto-scaling

### Configure Node Auto-provisioning

```bash
gcloud container clusters update honua-gke-cluster \
  --enable-autoprovisioning \
  --min-cpu 4 \
  --max-cpu 100 \
  --min-memory 16 \
  --max-memory 400 \
  --autoprovisioning-scopes=https://www.googleapis.com/auth/compute,https://www.googleapis.com/auth/devstorage.read_only,https://www.googleapis.com/auth/logging.write,https://www.googleapis.com/auth/monitoring
```

### Add Preemptible Node Pool for Cost Savings

```bash
gcloud container node-pools create preemptible-pool \
  --cluster honua-gke-cluster \
  --region $REGION \
  --machine-type n2-standard-4 \
  --preemptible \
  --num-nodes 0 \
  --enable-autoscaling \
  --min-nodes 0 \
  --max-nodes 10 \
  --node-labels workload=batch \
  --node-taints workload=batch:NoSchedule
```

## Security Best Practices

1. **Enable Binary Authorization** for container image verification
2. **Use Secret Manager** instead of Kubernetes secrets
3. **Enable GKE Security Posture** dashboard
4. **Configure VPC Service Controls**
5. **Enable Cloud Audit Logs**

### Enable Binary Authorization

```bash
gcloud container clusters update honua-gke-cluster \
  --region $REGION \
  --enable-binauthz
```

## Cost Optimization

1. Use **Committed Use Discounts** for predictable workloads
2. Enable **GKE Autopilot** for easier management (alternative deployment)
3. Use **Preemptible VMs** for batch processing
4. Monitor costs with **Cloud Billing Reports**
5. Set up **budget alerts**

### Create Budget Alert

```bash
gcloud billing budgets create \
  --billing-account=BILLING_ACCOUNT_ID \
  --display-name="Honua Monthly Budget" \
  --budget-amount=5000 \
  --threshold-rule=percent=50 \
  --threshold-rule=percent=90 \
  --threshold-rule=percent=100
```

## Troubleshooting

### Common Issues

1. **Workload Identity not working**: Verify service account bindings
2. **Load balancer not provisioning**: Check backend service health
3. **Persistent disk not attaching**: Verify zone configuration
4. **High costs**: Review resource requests/limits

### Useful Commands

```bash
# Check cluster status
gcloud container clusters describe honua-gke-cluster --region $REGION

# View node pool details
gcloud container node-pools list --cluster honua-gke-cluster --region $REGION

# Check operations
gcloud container operations list --region $REGION

# Force pod restart
kubectl rollout restart deployment/honua-api -n honua

# View Cloud Logging
gcloud logging read "resource.type=k8s_pod AND resource.labels.namespace_name=honua"
```

## Support Resources

- [GKE Documentation](https://cloud.google.com/kubernetes-engine/docs)
- [Google Cloud Operations](https://cloud.google.com/products/operations)
- [Workload Identity](https://cloud.google.com/kubernetes-engine/docs/how-to/workload-identity)
- [Cloud Armor](https://cloud.google.com/armor/docs)
