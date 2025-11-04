# Honua CI/CD Pipeline Setup Guide

## Overview

This guide provides comprehensive instructions for setting up and using the Honua CI/CD pipelines across multiple platforms (GitHub Actions, Azure DevOps, GitLab CI).

## Table of Contents

1. [Quick Start](#quick-start)
2. [Files Created](#files-created)
3. [Prerequisites](#prerequisites)
4. [Platform Setup](#platform-setup)
5. [Pipeline Triggers](#pipeline-triggers)
6. [Required Secrets](#required-secrets)
7. [Deployment Process](#deployment-process)
8. [Troubleshooting](#troubleshooting)

## Quick Start

### 1. Clone and Review

```bash
# Review all created pipeline files
ls -la .github/workflows/
ls -la .azure-pipelines/
ls -la .gitlab-ci.yml
ls -la scripts/
```

### 2. Configure Secrets

See [Required Secrets](#required-secrets) section below and the comprehensive [SECRETS_REFERENCE.md](docs/ci-cd/SECRETS_REFERENCE.md).

### 3. Test Locally

```bash
# Build multi-architecture images
./scripts/build-multi-arch.sh --name honua-server --version test

# Run tests
./scripts/run-tests.sh unit

# Test deployment (dry-run)
helm install honua-server ./deploy/helm/honua-server --dry-run
```

### 4. Enable Pipelines

- **GitHub Actions:** Already enabled (workflows in `.github/workflows/`)
- **Azure DevOps:** Import `azure-pipelines.yml`
- **GitLab CI:** `.gitlab-ci.yml` is automatically detected

## Files Created

### GitHub Actions Workflows (`.github/workflows/`)

| File | Purpose | Trigger |
|------|---------|---------|
| `build-and-push.yml` | Multi-arch image builds | Push to dev/main, tags |
| `deploy-dev.yml` | Development deployment | Push to dev |
| `deploy-staging.yml` | Staging deployment | Push to main |
| `deploy-production.yml` | Production deployment | Manual, tags |
| `helm-lint.yml` | Helm chart validation | PR, changes to helm/ |
| `release.yml` | Release creation | Tags (v*) |
| `cleanup-cache.yml` | Cache cleanup | Weekly, manual |

**Note:** Additional workflows already exist:
- `ci.yml` - Main CI pipeline
- `container-security.yml` - Container scanning
- `integration-tests.yml` - Integration tests
- `secret-rotation-deploy.yml` - Secret rotation
- And more...

### Azure DevOps Pipelines (`.azure-pipelines/`)

| File | Purpose |
|------|---------|
| `azure-pipelines.yml` | Main pipeline orchestration |
| `build-template.yml` | Build and test template |
| `deploy-template.yml` | Deployment template |
| `test-template.yml` | Test execution template |

### GitLab CI

| File | Purpose |
|------|---------|
| `.gitlab-ci.yml` | Complete GitLab CI configuration |

### Supporting Scripts (`scripts/`)

| Script | Purpose | Usage |
|--------|---------|-------|
| `build-multi-arch.sh` | Multi-architecture Docker builds | `./scripts/build-multi-arch.sh --help` |
| `push-to-registries.sh` | Push to multiple registries | `./scripts/push-to-registries.sh --help` |
| `deploy-k8s.sh` | Kubernetes deployment | `./scripts/deploy-k8s.sh ENV CLOUD TAG` |
| `run-tests.sh` | Test execution | `./scripts/run-tests.sh [test-type]` |

### Documentation (`docs/ci-cd/`)

| File | Purpose |
|------|---------|
| `README.md` | Complete CI/CD documentation |
| `SECRETS_REFERENCE.md` | Secrets configuration reference |

## Prerequisites

### Required Tools

1. **Development Tools:**
   - .NET SDK 9.0+
   - Docker 24.0+
   - Docker Buildx
   - Git

2. **Kubernetes Tools:**
   - kubectl 1.28+
   - Helm 3.13+

3. **Cloud Provider CLIs:**
   - AWS CLI (if using AWS)
   - Azure CLI (if using Azure)
   - gcloud SDK (if using GCP)

4. **Optional Tools:**
   - Trivy (security scanning)
   - Syft (SBOM generation)
   - Cosign (image signing)
   - Semgrep (SAST)

### Installation

**macOS:**
```bash
# Install Homebrew if needed
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install tools
brew install dotnet
brew install docker
brew install kubernetes-cli
brew install helm
brew install aws-cli
brew install azure-cli
brew install --cask google-cloud-sdk
brew install trivy
brew install syft
brew install cosign
```

**Ubuntu/Debian:**
```bash
# .NET SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0

# Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# kubectl
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl

# Helm
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# AWS CLI
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Trivy
wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | sudo apt-key add -
echo "deb https://aquasecurity.github.io/trivy-repo/deb $(lsb_release -sc) main" | sudo tee -a /etc/apt/sources.list.d/trivy.list
sudo apt-get update
sudo apt-get install trivy
```

**Windows:**
```powershell
# Install Chocolatey if needed
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

# Install tools
choco install dotnet-sdk
choco install docker-desktop
choco install kubernetes-cli
choco install kubernetes-helm
choco install awscli
choco install azure-cli
choco install gcloudsdk
```

## Platform Setup

### GitHub Actions

1. **Enable GitHub Actions** (if not already enabled):
   - Go to repository Settings → Actions → General
   - Enable "Allow all actions and reusable workflows"

2. **Configure Secrets:**
   - Go to Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - Add secrets from [Required Secrets](#required-secrets) section

3. **Configure Variables:**
   - Go to Settings → Secrets and variables → Actions → Variables
   - Click "New repository variable"
   - Add feature flags:
     ```
     ENABLE_AWS_ECR=true
     ENABLE_AZURE_ACR=true
     ENABLE_GCP_GCR=true
     ENABLE_SLACK_NOTIFICATIONS=true
     ENABLE_IMAGE_SIGNING=true
     ```

4. **Configure Environments:**
   - Go to Settings → Environments
   - Create environments: `development-aws`, `development-azure`, `development-gcp`, `staging-aws`, `staging-azure`, `staging-gcp`, `production-aws`, `production-azure`, `production-gcp`
   - For production environments, add:
     - Required reviewers
     - Deployment branch restrictions (only tags)

5. **Test Workflow:**
```bash
# Trigger CI workflow
git checkout -b test-ci
git commit --allow-empty -m "test: trigger CI"
git push origin test-ci
# Create PR to see workflow run
```

### Azure DevOps

1. **Create Project** (if needed):
   - Go to Azure DevOps
   - Create new project: "Honua"

2. **Import Repository:**
   - Repos → Import repository
   - Clone URL: Your GitHub repo URL

3. **Create Pipeline:**
   - Pipelines → New pipeline
   - Select "Azure Repos Git"
   - Select repository
   - Choose "Existing Azure Pipelines YAML file"
   - Path: `/.azure-pipelines/azure-pipelines.yml`

4. **Configure Service Connections:**
   - Project Settings → Service connections
   - Create connections for:
     - Azure Resource Manager (for each environment)
     - Docker Registry (for ACR)
     - GitHub (for releases)
     - AWS (if using)
     - GCP (if using)

5. **Create Variable Groups:**
   - Pipelines → Library → + Variable group
   - Create groups:
     - `honua-build-vars`
     - `honua-container-registry`
     - `honua-aws-dev`, `honua-aws-staging`, `honua-aws-prod`
     - `honua-azure-dev`, `honua-azure-staging`, `honua-azure-prod`
     - `honua-gcp-dev`, `honua-gcp-staging`, `honua-gcp-prod`

6. **Test Pipeline:**
   - Pipelines → Select pipeline → Run pipeline

### GitLab CI

1. **Configure Variables:**
   - Settings → CI/CD → Variables
   - Click "Add variable"
   - Add secrets from [Required Secrets](#required-secrets) section
   - Mark sensitive variables as "Masked" and "Protected"

2. **Configure Runners:**
   - Settings → CI/CD → Runners
   - Set up runners with Docker executor
   - Add tags: `docker`, `amd64`, `arm64` (for multi-arch)

3. **Configure Environments:**
   - Deployments → Environments
   - Create environments matching `.gitlab-ci.yml`

4. **Enable Auto DevOps (optional):**
   - Settings → CI/CD → Auto DevOps
   - Disable if using custom pipeline

5. **Test Pipeline:**
```bash
# Trigger pipeline
git checkout -b test-gitlab-ci
git commit --allow-empty -m "test: trigger GitLab CI"
git push origin test-gitlab-ci
```

## Pipeline Triggers

### Automatic Triggers

**GitHub Actions:**
```yaml
# Pull requests to main branches
on:
  pull_request:
    branches: [dev, main, master]

# Pushes to main branches
on:
  push:
    branches: [dev, main, master]

# Tags
on:
  push:
    tags: ['v*']
```

**Example Commands:**
```bash
# Trigger development deployment
git push origin dev

# Trigger staging deployment
git push origin main

# Trigger production deployment
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

### Manual Triggers

**GitHub Actions:**
1. Go to Actions tab
2. Select workflow
3. Click "Run workflow"
4. Select branch and fill parameters
5. Click "Run workflow"

**Azure DevOps:**
1. Go to Pipelines
2. Select pipeline
3. Click "Run pipeline"
4. Select branch and variables
5. Click "Run"

**GitLab CI:**
1. Go to CI/CD → Pipelines
2. Click "Run pipeline"
3. Select branch
4. Add variables if needed
5. Click "Run pipeline"

## Required Secrets

### Minimum Configuration

For a basic setup with GitHub Container Registry only:

```yaml
# Automatically provided
GITHUB_TOKEN: <automatic>
```

### Multi-Cloud Configuration

For full multi-cloud deployment, see the comprehensive [SECRETS_REFERENCE.md](docs/ci-cd/SECRETS_REFERENCE.md) document.

**Quick setup for each cloud:**

**AWS:**
```yaml
AWS_ACCESS_KEY_ID: <your-key>
AWS_SECRET_ACCESS_KEY: <your-secret>
AWS_REGION: us-east-1
AWS_EKS_CLUSTER_NAME_DEV: honua-dev
```

**Azure:**
```yaml
AZURE_CREDENTIALS: <service-principal-json>
AZURE_RESOURCE_GROUP_DEV: honua-dev-rg
AZURE_AKS_CLUSTER_NAME_DEV: honua-dev-aks
AZURE_KEYVAULT_NAME_DEV: honua-dev-kv
```

**GCP:**
```yaml
GCP_SERVICE_ACCOUNT_KEY: <service-account-json>
GCP_PROJECT_ID: my-project-id
GCP_REGION: us-central1
GCP_GKE_CLUSTER_NAME_DEV: honua-dev-gke
```

### Testing Secrets

```bash
# Test AWS
aws sts get-caller-identity

# Test Azure
az account show

# Test GCP
gcloud auth list
```

## Deployment Process

### Development Deployment

**Automatic:**
```bash
# Commit and push to dev branch
git checkout dev
git add .
git commit -m "feat: add new feature"
git push origin dev

# Monitors:
# - GitHub Actions: Actions tab
# - Azure DevOps: Pipelines
# - GitLab: CI/CD → Pipelines
```

**Manual:**
```bash
# GitHub Actions
# Go to Actions → deploy-dev.yml → Run workflow

# Using script
./scripts/deploy-k8s.sh dev aws ghcr.io/org/honua-server:latest
```

### Staging Deployment

**Automatic:**
```bash
# Merge PR to main
git checkout main
git merge dev
git push origin main

# Deployment starts automatically
# Monitor in Actions/Pipelines tab
```

**Manual:**
```bash
# GitHub Actions
# Go to Actions → deploy-staging.yml → Run workflow
# Enter image tag

# Using script
./scripts/deploy-k8s.sh staging azure myregistry.azurecr.io/honua-server:v1.0.0
```

### Production Deployment

**Only Manual:**
```bash
# GitHub Actions
# 1. Go to Actions
# 2. Select "Deploy to Production"
# 3. Click "Run workflow"
# 4. Enter:
#    - image_tag: v1.0.0
#    - cloud_provider: aws (or azure, gcp, all)
# 5. Click "Run workflow"
# 6. Approve when prompted

# Using script
./scripts/deploy-k8s.sh production gcp gcr.io/project/honua-server:v1.0.0
```

**Production Deployment Checklist:**
- [ ] Version deployed to staging
- [ ] Staging tests passed
- [ ] Security scans passed
- [ ] Database migrations reviewed
- [ ] Rollback plan prepared
- [ ] On-call team notified
- [ ] Change request approved

## Troubleshooting

### Common Issues

#### Build Failures

**Problem:** .NET restore fails
```bash
# Solution
dotnet nuget locals all --clear
dotnet restore --verbosity detailed
```

**Problem:** Docker build out of disk space
```bash
# Solution
docker system prune -af --volumes
```

#### Deployment Failures

**Problem:** kubectl cannot connect
```bash
# AWS
aws eks update-kubeconfig --region us-east-1 --name honua-dev

# Azure
az aks get-credentials --resource-group honua-dev-rg --name honua-dev-aks

# GCP
gcloud container clusters get-credentials honua-dev-gke --region us-central1
```

**Problem:** Helm timeout
```bash
# Check pod status
kubectl get pods -n honua-dev
kubectl describe pod <pod-name> -n honua-dev
kubectl logs <pod-name> -n honua-dev

# Increase timeout
helm upgrade --timeout 20m ...
```

#### Secret Issues

**Problem:** Secret not found
```bash
# Verify secret exists
# GitHub: Settings → Secrets and variables → Actions
# Azure DevOps: Pipelines → Library → Variable groups
# GitLab: Settings → CI/CD → Variables
```

**Problem:** Invalid credentials
```bash
# Test credentials locally
aws sts get-caller-identity
az account show
gcloud auth list
```

### Getting Help

**Documentation:**
- [Complete CI/CD Guide](docs/ci-cd/README.md)
- [Secrets Reference](docs/ci-cd/SECRETS_REFERENCE.md)
- [GitHub Actions Docs](https://docs.github.com/en/actions)
- [Azure Pipelines Docs](https://docs.microsoft.com/en-us/azure/devops/pipelines/)
- [GitLab CI Docs](https://docs.gitlab.com/ee/ci/)

**Support:**
- GitHub Issues: https://github.com/yourorg/honua/issues
- Slack: #honua-devops
- Email: devops@example.com

## Next Steps

1. **Configure Secrets**: Follow [SECRETS_REFERENCE.md](docs/ci-cd/SECRETS_REFERENCE.md)
2. **Test Pipelines**: Run a test deployment to dev
3. **Set Up Monitoring**: Configure alerts for pipeline failures
4. **Document Custom Workflows**: Add team-specific deployment procedures
5. **Schedule Reviews**: Set up quarterly pipeline reviews and secret rotation

## Summary of Created Files

### Workflow Files: 7 GitHub Actions workflows
- `.github/workflows/build-and-push.yml`
- `.github/workflows/deploy-dev.yml`
- `.github/workflows/deploy-staging.yml`
- `.github/workflows/deploy-production.yml`
- `.github/workflows/helm-lint.yml`
- `.github/workflows/release.yml`
- `.github/workflows/cleanup-cache.yml`

### Azure Pipelines: 4 YAML files
- `.azure-pipelines/azure-pipelines.yml`
- `.azure-pipelines/build-template.yml`
- `.azure-pipelines/deploy-template.yml`
- `.azure-pipelines/test-template.yml`

### GitLab CI: 1 configuration file
- `.gitlab-ci.yml`

### Scripts: 4 executable scripts
- `scripts/build-multi-arch.sh`
- `scripts/push-to-registries.sh`
- `scripts/deploy-k8s.sh`
- `scripts/run-tests.sh`

### Documentation: 3 markdown files
- `docs/ci-cd/README.md`
- `docs/ci-cd/SECRETS_REFERENCE.md`
- `CI_CD_SETUP_GUIDE.md` (this file)

### Total: 19 files created

---

**Last Updated:** December 2024
**Version:** 1.0.0
**Maintained By:** DevOps Team
