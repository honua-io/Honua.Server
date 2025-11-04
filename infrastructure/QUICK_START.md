# Honua Infrastructure - Quick Start Guide

Get Honua infrastructure up and running in 15 minutes.

## Prerequisites (5 minutes)

```bash
# Install Terraform
wget https://releases.hashicorp.com/terraform/1.6.6/terraform_1.6.6_linux_amd64.zip
unzip terraform_1.6.6_linux_amd64.zip
sudo mv terraform /usr/local/bin/

# Install kubectl
curl -LO "https://dl.k8s.io/release/v1.28.0/bin/linux/amd64/kubectl"
chmod +x kubectl
sudo mv kubectl /usr/local/bin/

# Install AWS CLI (or Azure CLI / gcloud for other clouds)
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# Configure AWS
aws configure
```

## Quick Deploy - AWS Development (10 minutes)

```bash
# 1. Clone repository
git clone https://github.com/HonuaIO/honua.git
cd honua/infrastructure

# 2. Create Terraform state backend
aws s3api create-bucket --bucket honua-terraform-state-dev --region us-east-1
aws s3api put-bucket-versioning --bucket honua-terraform-state-dev --versioning-configuration Status=Enabled
aws dynamodb create-table --table-name honua-terraform-locks --attribute-definitions AttributeName=LockID,AttributeType=S --key-schema AttributeName=LockID,KeyType=HASH --billing-mode PAY_PER_REQUEST

# 3. Configure variables
cd terraform/environments/dev
cp terraform.tfvars.example terraform.tfvars

cat > terraform.tfvars << 'TFVARS'
aws_region             = "us-east-1"
github_org             = "YourOrg"
github_repo            = "honua"
grafana_admin_password = "change-me-strong-password"
alarm_emails           = ["your-email@example.com"]
TFVARS

# 4. Deploy!
terraform init
terraform plan -out=tfplan
terraform apply tfplan

# 5. Configure kubectl
CLUSTER_NAME=$(terraform output -raw eks_cluster_name)
aws eks update-kubeconfig --name ${CLUSTER_NAME} --region us-east-1

# 6. Verify
kubectl get nodes
kubectl get pods -A
```

## Access Monitoring

```bash
# Forward Grafana
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80 &

# Access at http://localhost:3000
# Username: admin
# Password: <grafana_admin_password from terraform.tfvars>

# Forward Prometheus
kubectl port-forward -n monitoring svc/prometheus-kube-prometheus-prometheus 9090:9090 &

# Access at http://localhost:9090
```

## Deploy Your First Application

```bash
# Example deployment
kubectl create namespace honua
kubectl apply -f - << 'YAML'
apiVersion: apps/v1
kind: Deployment
metadata:
  name: hello-honua
  namespace: honua
spec:
  replicas: 3
  selector:
    matchLabels:
      app: hello-honua
  template:
    metadata:
      labels:
        app: hello-honua
    spec:
      containers:
      - name: nginx
        image: nginx:latest
        ports:
        - containerPort: 80
---
apiVersion: v1
kind: Service
metadata:
  name: hello-honua
  namespace: honua
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 80
  selector:
    app: hello-honua
YAML

# Get load balancer URL
kubectl get svc -n honua hello-honua
```

## Create Customer Resources

```bash
# Provision customer with dedicated registry and credentials
cd ../../../scripts
./provision-customer.sh dev customer-123 "Test Customer"

# This creates:
# - ECR repository
# - IAM user
# - Access credentials
# - Stored in Secrets Manager
```

## Clean Up (Optional)

```bash
# To destroy the environment
cd ../terraform/environments/dev
terraform destroy

# Or use the safe destroy script
cd ../../../scripts
./destroy-env.sh dev
```

## What You Get

- ✅ Kubernetes cluster with 2 ARM nodes
- ✅ PostgreSQL database (50GB)
- ✅ Redis cache
- ✅ Container registry
- ✅ Private networking
- ✅ Prometheus + Grafana monitoring
- ✅ Budget alerts
- ✅ Automated backups

**Monthly Cost**: ~$500-800 (AWS dev environment with spot instances)

## Next Steps

1. **Deploy Applications**: Push images to ECR, deploy to Kubernetes
2. **Set Up CI/CD**: Configure GitHub Actions with OIDC
3. **Add Monitoring**: Import Grafana dashboards
4. **Scale to Staging**: Deploy staging environment
5. **Plan Production**: Review production configuration

## Get Help

- 📖 Full documentation: `infrastructure/README.md`
- 🚀 Deployment guide: `infrastructure/DEPLOYMENT_GUIDE.md`
- 💰 Cost breakdown: `infrastructure/COST_ESTIMATION.md`
- 🏗️ Architecture: `infrastructure/INFRASTRUCTURE_SUMMARY.md`

## Common Issues

**State lock error**:
```bash
terraform force-unlock <lock-id>
```

**kubectl connection fails**:
```bash
aws eks update-kubeconfig --name honua-dev --region us-east-1
```

**Deployment takes too long**:
- First deployment: 20-30 minutes (normal)
- Cluster provisioning: 10-15 minutes
- Database setup: 5-10 minutes

---

**Ready to get started?** Run the commands above and you'll have a working infrastructure in minutes!
