---
tags: [kubernetes, k8s, deployment, helm, scaling, hpa, production, orchestration]
category: deployment
difficulty: advanced
version: 1.0.0
last_updated: 2025-10-15
---

# Kubernetes Deployment Complete Guide

Comprehensive guide to deploying Honua on Kubernetes with Helm charts, autoscaling, and production best practices.

## Table of Contents
- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Deployment Manifests](#deployment-manifests)
- [Helm Chart](#helm-chart)
- [Horizontal Pod Autoscaling](#horizontal-pod-autoscaling)
- [Persistent Storage](#persistent-storage)
- [Configuration](#configuration)
- [Monitoring](#monitoring)
- [Production Checklist](#production-checklist)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua can be deployed on any Kubernetes cluster including:
- On-premises clusters
- Amazon EKS
- Azure AKS
- Google GKE
- DigitalOcean Kubernetes
- Managed K8s providers

### Architecture

```
┌──────────────┐
│   Ingress    │
└──────┬───────┘
       │
┌──────▼───────┐     ┌────────────┐
│   Service    │────▶│  Honua     │
└──────────────┘     │  Pods      │
                     └─────┬──────┘
                           │
                     ┌─────▼──────┐
                     │ PostgreSQL │
                     │ PostGIS    │
                     └────────────┘
```

## Prerequisites

- Kubernetes 1.24+
- kubectl configured
- Helm 3.0+ (optional)
- PostgreSQL/PostGIS database
- Redis (optional, for caching)
- Storage class for PVCs

## Quick Start

### Using kubectl

```bash
# Create namespace
kubectl create namespace honua

# Apply manifests
kubectl apply -f k8s/

# Check deployment
kubectl get pods -n honua
kubectl get svc -n honua
```

### Using Helm

```bash
# Add Honua Helm repo (when available)
helm repo add honua https://charts.honua.io
helm repo update

# Install Honua
helm install honua honua/honua \
  --namespace honua \
  --create-namespace \
  --set database.host=postgis.honua.svc.cluster.local \
  --set database.password=CHANGE_ME

# Check status
helm status honua -n honua
```

## Deployment Manifests

### Complete Kubernetes Deployment

Create `k8s/` directory with these files:

#### 00-namespace.yaml

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: honua
  labels:
    name: honua
    environment: production
```

#### 01-deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua
  labels:
    app: honua-server
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      containers:
      - name: honua
        image: honuaio/honua-server:1.0.0
        ports:
        - name: http
          containerPort: 8080
          protocol: TCP
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        - name: honua__database__host
          value: "postgis-service.honua.svc.cluster.local"
        - name: honua__database__port
          value: "5432"
        - name: honua__database__database
          value: "honua"
        - name: honua__database__username
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: db-username
        - name: honua__database__password
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: db-password
        - name: honua__authentication__jwt__signingKey
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: jwt-signing-key
        - name: honua__cache__enabled
          value: "true"
        - name: honua__cache__provider
          value: "Redis"
        - name: honua__cache__redis__host
          value: "redis-service.honua.svc.cluster.local"
        - name: honua__cache__redis__port
          value: "6379"
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: http
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: http
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
        volumeMounts:
        - name: metadata
          mountPath: /app/metadata
        - name: data
          mountPath: /app/data
      volumes:
      - name: metadata
        persistentVolumeClaim:
          claimName: honua-metadata-pvc
      - name: data
        persistentVolumeClaim:
          claimName: honua-data-pvc
```

#### 02-service.yaml

```yaml
apiVersion: v1
kind: Service
metadata:
  name: honua-service
  namespace: honua
  labels:
    app: honua-server
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: http
    protocol: TCP
    name: http
  selector:
    app: honua-server
```

#### 03-ingress.yaml

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua-ingress
  namespace: honua
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/proxy-body-size: "100m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "600"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - honua.example.com
    secretName: honua-tls
  rules:
  - host: honua.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: honua-service
            port:
              number: 80
```

#### 04-secrets.yaml

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: honua-secrets
  namespace: honua
type: Opaque
stringData:
  db-username: honua_user
  db-password: CHANGE_ME_IN_PRODUCTION
  jwt-signing-key: CHANGE_ME_GENERATE_STRONG_KEY
```

**Generate secrets:**
```bash
# Generate JWT signing key
openssl rand -base64 32

# Create secret
kubectl create secret generic honua-secrets \
  --from-literal=db-username=honua_user \
  --from-literal=db-password=$(openssl rand -base64 24) \
  --from-literal=jwt-signing-key=$(openssl rand -base64 32) \
  -n honua
```

#### 05-pvc.yaml

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-metadata-pvc
  namespace: honua
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 10Gi
  storageClassName: standard
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-data-pvc
  namespace: honua
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 100Gi
  storageClassName: standard
```

#### 06-configmap.yaml

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua
data:
  appsettings.Production.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "honua": {
        "workspacePath": "/app/metadata",
        "authentication": {
          "mode": "Jwt",
          "enforce": true
        },
        "observability": {
          "metrics": {
            "enabled": true,
            "endpoint": "/metrics"
          }
        }
      }
    }
```

## Horizontal Pod Autoscaling

### HPA Configuration

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-hpa
  namespace: honua
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-server
  minReplicas: 3
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
      - type: Pods
        value: 4
        periodSeconds: 30
      selectPolicy: Max
```

**Apply HPA:**
```bash
kubectl apply -f hpa.yaml
kubectl get hpa -n honua
```

## Helm Chart

### values.yaml

```yaml
replicaCount: 3

image:
  repository: honuaio/honua-server
  pullPolicy: IfNotPresent
  tag: "1.0.0"

service:
  type: ClusterIP
  port: 80

ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
  hosts:
    - host: honua.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: honua-tls
      hosts:
        - honua.example.com

resources:
  limits:
    cpu: 2000m
    memory: 2Gi
  requests:
    cpu: 500m
    memory: 1Gi

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 20
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80

database:
  host: postgis-service
  port: 5432
  database: honua
  username: honua_user
  password: ""  # Set via --set or secrets

redis:
  enabled: true
  host: redis-service
  port: 6379

persistence:
  enabled: true
  metadata:
    size: 10Gi
    storageClass: standard
  data:
    size: 100Gi
    storageClass: standard
```

**Install with custom values:**
```bash
helm install honua ./helm/honua \
  -n honua \
  --create-namespace \
  -f values-production.yaml \
  --set database.password=$(kubectl get secret db-secret -o jsonpath='{.data.password}' | base64 -d)
```

## Persistent Storage

### Storage Classes

**AWS EBS:**
```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: honua-fast
provisioner: ebs.csi.aws.com
parameters:
  type: gp3
  iops: "3000"
  throughput: "125"
allowVolumeExpansion: true
```

**Azure Disk:**
```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: honua-fast
provisioner: disk.csi.azure.com
parameters:
  skuName: Premium_LRS
allowVolumeExpansion: true
```

**GCP Persistent Disk:**
```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: honua-fast
provisioner: pd.csi.storage.gke.io
parameters:
  type: pd-ssd
allowVolumeExpansion: true
```

### ReadWriteMany for Multi-Pod

For metadata shared across pods:

**AWS EFS:**
```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-metadata-pvc
spec:
  accessModes:
    - ReadWriteMany
  storageClassName: efs-sc
  resources:
    requests:
      storage: 10Gi
```

**Azure Files:**
```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-metadata-pvc
spec:
  accessModes:
    - ReadWriteMany
  storageClassName: azurefile
  resources:
    requests:
      storage: 10Gi
```

## Configuration

### Environment-Specific Configs

**Development:**
```yaml
replicaCount: 1
resources:
  requests:
    cpu: 250m
    memory: 512Mi
  limits:
    cpu: 500m
    memory: 1Gi
autoscaling:
  enabled: false
```

**Staging:**
```yaml
replicaCount: 2
resources:
  requests:
    cpu: 500m
    memory: 1Gi
  limits:
    cpu: 1000m
    memory: 2Gi
autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
```

**Production:**
```yaml
replicaCount: 3
resources:
  requests:
    cpu: 1000m
    memory: 2Gi
  limits:
    cpu: 2000m
    memory: 4Gi
autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 20
```

## Monitoring

### ServiceMonitor (Prometheus Operator)

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: honua-metrics
  namespace: honua
  labels:
    app: honua-server
spec:
  selector:
    matchLabels:
      app: honua-server
  endpoints:
  - port: http
    path: /metrics
    interval: 30s
```

### Grafana Dashboard

Import dashboard from Honua docs or create custom:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-dashboard
  namespace: monitoring
  labels:
    grafana_dashboard: "1"
data:
  honua.json: |
    {
      "dashboard": {...}
    }
```

## Production Checklist

- [ ] Use secrets management (Sealed Secrets, Vault, etc.)
- [ ] Configure resource requests/limits
- [ ] Enable HPA with appropriate thresholds
- [ ] Set up ingress with TLS
- [ ] Configure persistent storage (ReadWriteMany for metadata)
- [ ] Deploy PostgreSQL with HA (CrunchyData, Patroni)
- [ ] Deploy Redis cluster
- [ ] Set up monitoring (Prometheus + Grafana)
- [ ] Configure logging (ELK, Loki)
- [ ] Enable network policies
- [ ] Set pod disruption budgets
- [ ] Configure backups (Velero)
- [ ] Test disaster recovery
- [ ] Document runbooks

## Troubleshooting

### Pods Not Starting

```bash
# Check pod status
kubectl get pods -n honua

# View logs
kubectl logs -n honua deployment/honua-server --tail=100

# Describe pod
kubectl describe pod -n honua <pod-name>

# Check events
kubectl get events -n honua --sort-by='.lastTimestamp'
```

### Database Connection Issues

```bash
# Test database connectivity
kubectl run -it --rm debug --image=postgres:15 --restart=Never -n honua -- \
  psql -h postgis-service -U honua_user -d honua

# Check secret
kubectl get secret honua-secrets -n honua -o jsonpath='{.data.db-password}' | base64 -d
```

### HPA Not Scaling

```bash
# Check HPA status
kubectl get hpa -n honua
kubectl describe hpa honua-hpa -n honua

# Verify metrics server
kubectl top nodes
kubectl top pods -n honua
```

## Related Documentation

- [Docker Deployment](./04-01-docker-deployment.md) - Container basics
- [Configuration Reference](./02-01-configuration-reference.md) - Settings
- [Monitoring](./02-01-configuration-reference.md#observability) - Observability
- [Common Issues](./05-02-common-issues.md) - Troubleshooting

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**Kubernetes**: 1.24+
