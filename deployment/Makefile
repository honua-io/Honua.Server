# Makefile for Honua Deployment Operations
# Provides convenient commands for building, deploying, and managing the application

.PHONY: help build push deploy clean test

# Variables
PROJECT_NAME := honua
DOCKER_REGISTRY ?= docker.io
IMAGE_TAG ?= latest
NAMESPACE ?= honua
ENVIRONMENT ?= development
CLOUD_PROVIDER ?= generic

# Docker image names
API_IMAGE := $(DOCKER_REGISTRY)/honua/server-host
INTAKE_IMAGE := $(DOCKER_REGISTRY)/honua/server-intake
ORCHESTRATOR_IMAGE := $(DOCKER_REGISTRY)/honua/build-orchestrator

# Help target
help: ## Show this help message
	@echo "Honua Deployment Makefile"
	@echo ""
	@echo "Usage: make [target] [VARIABLE=value]"
	@echo ""
	@echo "Variables:"
	@echo "  DOCKER_REGISTRY   Docker registry (default: docker.io)"
	@echo "  IMAGE_TAG         Image tag (default: latest)"
	@echo "  NAMESPACE         Kubernetes namespace (default: honua)"
	@echo "  ENVIRONMENT       Environment (development/staging/production)"
	@echo "  CLOUD_PROVIDER    Cloud provider (aws/azure/gcp/generic)"
	@echo ""
	@echo "Targets:"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-20s %s\n", $$1, $$2}'

# Docker operations
build: ## Build all Docker images
	@echo "Building Docker images..."
	docker build -f deployment/docker/Dockerfile.host -t $(API_IMAGE):$(IMAGE_TAG) .
	docker build -f deployment/docker/Dockerfile.intake -t $(INTAKE_IMAGE):$(IMAGE_TAG) .
	docker build -f deployment/docker/Dockerfile.orchestrator -t $(ORCHESTRATOR_IMAGE):$(IMAGE_TAG) .
	@echo "Build complete!"

build-api: ## Build API server image
	docker build -f deployment/docker/Dockerfile.host -t $(API_IMAGE):$(IMAGE_TAG) .

build-intake: ## Build intake service image
	docker build -f deployment/docker/Dockerfile.intake -t $(INTAKE_IMAGE):$(IMAGE_TAG) .

build-orchestrator: ## Build orchestrator image
	docker build -f deployment/docker/Dockerfile.orchestrator -t $(ORCHESTRATOR_IMAGE):$(IMAGE_TAG) .

push: ## Push all Docker images to registry
	@echo "Pushing images to $(DOCKER_REGISTRY)..."
	docker push $(API_IMAGE):$(IMAGE_TAG)
	docker push $(INTAKE_IMAGE):$(IMAGE_TAG)
	docker push $(ORCHESTRATOR_IMAGE):$(IMAGE_TAG)
	@echo "Push complete!"

build-and-push: build push ## Build and push all images

# Local development
dev-up: ## Start local development environment
	docker-compose -f deployment/docker-compose.yml up -d
	@echo "Development environment is running!"
	@echo "API: http://localhost:8080"
	@echo "Intake: http://localhost:8082"

dev-down: ## Stop local development environment
	docker-compose -f deployment/docker-compose.yml down

dev-logs: ## View logs from local development
	docker-compose -f deployment/docker-compose.yml logs -f

dev-restart: ## Restart local development environment
	docker-compose -f deployment/docker-compose.yml restart

monitoring-up: ## Start monitoring stack
	docker-compose -f deployment/docker-compose.monitoring.yml up -d
	@echo "Monitoring stack is running!"
	@echo "Prometheus: http://localhost:9090"
	@echo "Grafana: http://localhost:3000 (admin/admin)"

monitoring-down: ## Stop monitoring stack
	docker-compose -f deployment/docker-compose.monitoring.yml down

# Kubernetes operations
k8s-namespace: ## Create Kubernetes namespace
	kubectl create namespace $(NAMESPACE) --dry-run=client -o yaml | kubectl apply -f -

k8s-deploy-base: k8s-namespace ## Deploy using Kustomize base
	kubectl apply -k deployment/k8s/base

k8s-deploy-dev: k8s-namespace ## Deploy development environment
	kubectl apply -k deployment/k8s/overlays/development

k8s-deploy-staging: k8s-namespace ## Deploy staging environment
	kubectl apply -k deployment/k8s/overlays/staging

k8s-deploy-prod: k8s-namespace ## Deploy production environment
	kubectl apply -k deployment/k8s/overlays/production

k8s-delete: ## Delete Kubernetes deployment
	kubectl delete namespace $(NAMESPACE)

k8s-logs-api: ## View API logs
	kubectl logs -n $(NAMESPACE) -l app.kubernetes.io/component=api -f

k8s-logs-intake: ## View intake logs
	kubectl logs -n $(NAMESPACE) -l app.kubernetes.io/component=intake -f

k8s-logs-orchestrator: ## View orchestrator logs
	kubectl logs -n $(NAMESPACE) -l app.kubernetes.io/component=orchestrator -f

k8s-status: ## Check deployment status
	kubectl get all -n $(NAMESPACE)

k8s-describe: ## Describe all resources
	kubectl describe all -n $(NAMESPACE)

# Helm operations
helm-install: ## Install using Helm
	helm install $(PROJECT_NAME) deployment/helm/honua \
		-n $(NAMESPACE) \
		--create-namespace \
		-f deployment/helm/honua/values-$(ENVIRONMENT).yaml

helm-upgrade: ## Upgrade Helm release
	helm upgrade $(PROJECT_NAME) deployment/helm/honua \
		-n $(NAMESPACE) \
		-f deployment/helm/honua/values-$(ENVIRONMENT).yaml

helm-uninstall: ## Uninstall Helm release
	helm uninstall $(PROJECT_NAME) -n $(NAMESPACE)

helm-template: ## Generate Helm templates
	helm template $(PROJECT_NAME) deployment/helm/honua \
		-n $(NAMESPACE) \
		-f deployment/helm/honua/values-$(ENVIRONMENT).yaml

helm-lint: ## Lint Helm chart
	helm lint deployment/helm/honua

# Cloud-specific deployments
deploy-aws: ## Deploy to AWS EKS
	@echo "Deploying to AWS EKS..."
	kubectl apply -f deployment/cloud/aws/eks-config.yaml
	$(MAKE) helm-install ENVIRONMENT=production CLOUD_PROVIDER=aws

deploy-azure: ## Deploy to Azure AKS
	@echo "Deploying to Azure AKS..."
	kubectl apply -f deployment/cloud/azure/aks-config.yaml
	$(MAKE) helm-install ENVIRONMENT=production CLOUD_PROVIDER=azure

deploy-gcp: ## Deploy to GCP GKE
	@echo "Deploying to GCP GKE..."
	kubectl apply -f deployment/cloud/gcp/gke-config.yaml
	$(MAKE) helm-install ENVIRONMENT=production CLOUD_PROVIDER=gcp

# Testing
test-connection: ## Test database and Redis connections
	@echo "Testing PostgreSQL connection..."
	@docker exec honua-postgres pg_isready -U honua -d honua || echo "PostgreSQL not ready"
	@echo "Testing Redis connection..."
	@docker exec honua-redis redis-cli -a honua_dev_password ping || echo "Redis not ready"

# Cleanup
clean: ## Clean up Docker resources
	docker system prune -f
	docker volume prune -f

clean-all: ## Clean up all Docker resources (including images)
	docker system prune -a -f
	docker volume prune -f

# Database operations
db-migrate: ## Run database migrations
	kubectl exec -n $(NAMESPACE) -it deployment/$(PROJECT_NAME)-api -- dotnet ef database update

db-backup: ## Backup database
	kubectl exec -n $(NAMESPACE) deployment/postgres-0 -- pg_dump -U honua honua > backup-$$(date +%Y%m%d-%H%M%S).sql

# Monitoring and debugging
port-forward-api: ## Port forward to API service
	kubectl port-forward -n $(NAMESPACE) svc/$(PROJECT_NAME)-api 8080:8080

port-forward-prometheus: ## Port forward to Prometheus
	kubectl port-forward -n $(NAMESPACE) svc/prometheus 9090:9090

port-forward-grafana: ## Port forward to Grafana
	kubectl port-forward -n $(NAMESPACE) svc/grafana 3000:3000

shell-api: ## Open shell in API pod
	kubectl exec -n $(NAMESPACE) -it deployment/$(PROJECT_NAME)-api -- /bin/sh

shell-postgres: ## Open PostgreSQL shell
	kubectl exec -n $(NAMESPACE) -it statefulset/postgres -- psql -U honua -d honua

# Version management
version: ## Show current version
	@grep "version:" deployment/helm/honua/Chart.yaml | awk '{print $$2}'

bump-version: ## Bump patch version
	@current=$$(grep "version:" deployment/helm/honua/Chart.yaml | awk '{print $$2}'); \
	major=$$(echo $$current | cut -d. -f1); \
	minor=$$(echo $$current | cut -d. -f2); \
	patch=$$(echo $$current | cut -d. -f3); \
	new_patch=$$((patch + 1)); \
	new_version="$$major.$$minor.$$new_patch"; \
	sed -i "s/version: $$current/version: $$new_version/" deployment/helm/honua/Chart.yaml; \
	echo "Version bumped to $$new_version"

# CI/CD
ci-build: ## CI: Build and test
	$(MAKE) build
	$(MAKE) test-connection

ci-deploy: ## CI: Deploy to production
	$(MAKE) build-and-push IMAGE_TAG=$${CI_COMMIT_SHA:-latest}
	$(MAKE) helm-upgrade ENVIRONMENT=production IMAGE_TAG=$${CI_COMMIT_SHA:-latest}

# Documentation
docs-serve: ## Serve documentation locally
	@echo "Serving documentation at http://localhost:8000"
	@cd docs && python3 -m http.server 8000
