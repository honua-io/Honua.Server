# Kubernetes Deployment Guide

**Keywords**: kubernetes, k8s, helm, deployment, statefulset, configmap, secrets, ingress, hpa, persistent-volumes, networking, service-mesh
**Related**: docker-deployment, aws-ecs-deployment, environment-variables, monitoring-observability

## Overview

Honua is designed for cloud-native deployment on Kubernetes with support for high availability, auto-scaling, and production-grade configurations. This guide covers deployment patterns from simple single-pod setups to complex multi-region architectures.

**Key Features**:
- Horizontal Pod Autoscaling (HPA) for dynamic scaling
- StatefulSet for PostGIS database with persistent storage
- ConfigMaps and Secrets for configuration management
- Ingress controllers for external access
- Network Policies for security
- Health checks and readiness probes
- Resource limits and quotas
- Multi-availability zone support

## Quick Start

### Prerequisites

```bash
# Verify kubectl is installed
kubectl version --client

# Verify cluster access
kubectl cluster-info

# Create namespace
kubectl create namespace honua
```

### Minimal Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua
        image: honua:latest
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: HONUA__METADATA__PROVIDER
          value: "json"
        - name: HONUA__METADATA__PATH
          value: "/app/config/metadata.json"
        - name: HONUA__AUTHENTICATION__MODE
          value: "QuickStart"
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: honua-server
  namespace: honua
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
  selector:
    app: honua-server
```

Apply with:

```bash
kubectl apply -f honua-deployment.yaml

# Wait for deployment
kubectl wait --for=condition=Available deployment/honua-server -n honua --timeout=300s

# Get service external IP
kubectl get svc honua-server -n honua
```

## Production Deployment

### Complete Stack with PostGIS

Full production deployment with database, caching, and monitoring:

```yaml
---
# Namespace
apiVersion: v1
kind: Namespace
metadata:
  name: honua-prod
  labels:
    name: honua-prod
    environment: production

---
# ConfigMap for Honua configuration
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua-prod
data:
  # Metadata configuration
  HONUA__METADATA__PROVIDER: "database"
  HONUA__METADATA__PATH: "/app/config"

  # OData configuration
  HONUA__ODATA__ENABLED: "true"
  HONUA__ODATA__DEFAULTPAGESIZE: "100"
  HONUA__ODATA__MAXPAGESIZE: "1000"

  # Service configuration
  HONUA__SERVICES__WFS__ENABLED: "true"
  HONUA__SERVICES__WMS__ENABLED: "true"
  HONUA__SERVICES__GEOMETRY__ENABLED: "true"
  HONUA__SERVICES__STAC__ENABLED: "true"

  # Logging
  Serilog__MinimumLevel__Default: "Information"
  Serilog__MinimumLevel__Override__Microsoft: "Warning"

  # Observability
  observability__metrics__enabled: "true"
  observability__metrics__endpoint: "/metrics"
  observability__metrics__usePrometheus: "true"

---
# Secret for database credentials
apiVersion: v1
kind: Secret
metadata:
  name: honua-db-secret
  namespace: honua-prod
type: Opaque
stringData:
  username: honua
  password: <CHANGE_ME_SECURE_PASSWORD>
  connection-string: |
    Host=postgis-0.postgis.honua-prod.svc.cluster.local;
    Port=5432;
    Database=honuadb;
    Username=honua;
    Password=<CHANGE_ME_SECURE_PASSWORD>;
    Pooling=true;
    MinPoolSize=10;
    MaxPoolSize=100;
    ConnectionIdleLifetime=300;
    CommandTimeout=60;

---
# PostGIS StatefulSet with persistent storage
apiVersion: v1
kind: Service
metadata:
  name: postgis
  namespace: honua-prod
  labels:
    app: postgis
spec:
  ports:
  - port: 5432
    name: postgres
  clusterIP: None  # Headless service for StatefulSet
  selector:
    app: postgis

---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgis
  namespace: honua-prod
spec:
  serviceName: postgis
  replicas: 1
  selector:
    matchLabels:
      app: postgis
  template:
    metadata:
      labels:
        app: postgis
    spec:
      securityContext:
        fsGroup: 999  # PostgreSQL group
      containers:
      - name: postgis
        image: postgis/postgis:16-3.4
        ports:
        - containerPort: 5432
          name: postgres
        env:
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: honua-db-secret
              key: username
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: honua-db-secret
              key: password
        - name: POSTGRES_DB
          value: honuadb
        - name: POSTGRES_INITDB_ARGS
          value: "--encoding=UTF8 --locale=en_US.UTF-8"
        - name: PGDATA
          value: /var/lib/postgresql/data/pgdata
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        volumeMounts:
        - name: postgis-data
          mountPath: /var/lib/postgresql/data
        - name: init-scripts
          mountPath: /docker-entrypoint-initdb.d
          readOnly: true
        livenessProbe:
          exec:
            command:
            - /bin/sh
            - -c
            - pg_isready -U honua -d honuadb
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          exec:
            command:
            - /bin/sh
            - -c
            - pg_isready -U honua -d honuadb && psql -U honua -d honuadb -c "SELECT 1"
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
      volumes:
      - name: init-scripts
        configMap:
          name: postgis-init-scripts
          optional: true
  volumeClaimTemplates:
  - metadata:
      name: postgis-data
    spec:
      accessModes: ["ReadWriteOnce"]
      storageClassName: fast-ssd  # Use your cluster's storage class
      resources:
        requests:
          storage: 100Gi

---
# Honua Server Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua-prod
  labels:
    app: honua-server
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0  # Zero-downtime deployments
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
        version: v1
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
          - weight: 100
            podAffinityTerm:
              labelSelector:
                matchExpressions:
                - key: app
                  operator: In
                  values:
                  - honua-server
              topologyKey: kubernetes.io/hostname
      securityContext:
        runAsNonRoot: true
        runAsUser: 1654  # app user from chiseled image
        runAsGroup: 1654
        fsGroup: 1654
      containers:
      - name: honua
        image: honua:latest  # Use specific version tag in production
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8080
          name: http
          protocol: TCP
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        - name: HONUA__DATABASE__CONNECTIONSTRING
          valueFrom:
            secretKeyRef:
              name: honua-db-secret
              key: connection-string
        - name: HONUA__AUTHENTICATION__MODE
          value: "OAuth"  # Production auth
        - name: HONUA__AUTHENTICATION__ENFORCE
          value: "true"
        envFrom:
        - configMapRef:
            name: honua-config
        volumeMounts:
        - name: tile-cache
          mountPath: /app/tiles
        - name: attachments
          mountPath: /app/attachments
        - name: tmp
          mountPath: /tmp
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: http
            scheme: HTTP
          initialDelaySeconds: 30
          periodSeconds: 30
          timeoutSeconds: 10
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health
            port: http
            scheme: HTTP
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        startupProbe:
          httpGet:
            path: /health
            port: http
            scheme: HTTP
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 30  # 150 seconds to start
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          readOnlyRootFilesystem: true
      volumes:
      - name: tile-cache
        persistentVolumeClaim:
          claimName: honua-tile-cache
      - name: attachments
        persistentVolumeClaim:
          claimName: honua-attachments
      - name: tmp
        emptyDir: {}

---
# Service for Honua Server
apiVersion: v1
kind: Service
metadata:
  name: honua-server
  namespace: honua-prod
  labels:
    app: honua-server
spec:
  type: ClusterIP
  sessionAffinity: None
  ports:
  - port: 80
    targetPort: http
    protocol: TCP
    name: http
  selector:
    app: honua-server

---
# PersistentVolumeClaim for tile cache
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-tile-cache
  namespace: honua-prod
spec:
  accessModes:
  - ReadWriteMany  # Multiple pods need access
  storageClassName: efs-sc  # Use EFS, Azure Files, or NFS
  resources:
    requests:
      storage: 50Gi

---
# PersistentVolumeClaim for attachments
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-attachments
  namespace: honua-prod
spec:
  accessModes:
  - ReadWriteMany
  storageClassName: efs-sc
  resources:
    requests:
      storage: 100Gi

---
# Horizontal Pod Autoscaler
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-server-hpa
  namespace: honua-prod
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
      stabilizationWindowSeconds: 300  # 5 min cooldown
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0  # Scale up immediately
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
      - type: Pods
        value: 4
        periodSeconds: 30
      selectPolicy: Max

---
# Ingress with SSL termination
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua-ingress
  namespace: honua-prod
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/limit-rps: "50"
    nginx.ingress.kubernetes.io/proxy-body-size: "50m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"
    nginx.ingress.kubernetes.io/enable-cors: "true"
    nginx.ingress.kubernetes.io/cors-allow-origin: "*"
    nginx.ingress.kubernetes.io/cors-allow-methods: "GET, POST, PUT, DELETE, OPTIONS"
spec:
  tls:
  - hosts:
    - honua.example.com
    secretName: honua-tls-cert
  rules:
  - host: honua.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: honua-server
            port:
              number: 80

---
# Network Policy - Allow ingress from ingress controller only
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: honua-server-netpol
  namespace: honua-prod
spec:
  podSelector:
    matchLabels:
      app: honua-server
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
  egress:
  # Allow DNS
  - to:
    - namespaceSelector:
        matchLabels:
          name: kube-system
    ports:
    - protocol: UDP
      port: 53
  # Allow PostgreSQL
  - to:
    - podSelector:
        matchLabels:
          app: postgis
    ports:
    - protocol: TCP
      port: 5432
  # Allow external HTTPS (for OAuth, etc.)
  - to:
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 443

---
# PodDisruptionBudget for high availability
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: honua-server-pdb
  namespace: honua-prod
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: honua-server
```

### Apply the Configuration

```bash
# Create the namespace and resources
kubectl apply -f honua-production.yaml

# Watch deployment progress
kubectl get pods -n honua-prod -w

# Check service status
kubectl get svc,deployment,statefulset,pvc,hpa -n honua-prod
```

## Helm Chart Deployment

### Helm Chart Structure

```
honua-chart/
├── Chart.yaml
├── values.yaml
├── values-prod.yaml
├── values-staging.yaml
├── templates/
│   ├── namespace.yaml
│   ├── configmap.yaml
│   ├── secret.yaml
│   ├── postgis-statefulset.yaml
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   ├── hpa.yaml
│   ├── pvc.yaml
│   ├── networkpolicy.yaml
│   ├── pdb.yaml
│   └── servicemonitor.yaml
└── README.md
```

### values.yaml

```yaml
# Honua Helm Chart Values

global:
  namespace: honua
  environment: production

image:
  repository: honua/server
  tag: "1.0.0"
  pullPolicy: IfNotPresent

replicaCount: 3

resources:
  limits:
    cpu: 1000m
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

ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rate-limit: "100"
  hosts:
    - host: honua.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: honua-tls
      hosts:
        - honua.example.com

service:
  type: ClusterIP
  port: 80
  targetPort: 8080

config:
  metadata:
    provider: database
    path: /app/config
  authentication:
    mode: OAuth
    enforce: true
  odata:
    enabled: true
    defaultPageSize: 100
    maxPageSize: 1000
  services:
    wfs:
      enabled: true
    wms:
      enabled: true
    geometry:
      enabled: true
    stac:
      enabled: true
  observability:
    metrics:
      enabled: true
      endpoint: /metrics

postgis:
  enabled: true
  image:
    repository: postgis/postgis
    tag: 16-3.4
  persistence:
    enabled: true
    storageClass: fast-ssd
    size: 100Gi
  resources:
    limits:
      cpu: 2000m
      memory: 4Gi
    requests:
      cpu: 500m
      memory: 1Gi
  database: honuadb
  username: honua
  # Password should be set via --set or sealed secrets
  # password: <set-via-values-override>

persistence:
  tileCache:
    enabled: true
    storageClass: efs-sc
    size: 50Gi
    accessMode: ReadWriteMany
  attachments:
    enabled: true
    storageClass: efs-sc
    size: 100Gi
    accessMode: ReadWriteMany

networkPolicy:
  enabled: true
  policyTypes:
    - Ingress
    - Egress

podDisruptionBudget:
  enabled: true
  minAvailable: 2

monitoring:
  prometheus:
    enabled: true
    serviceMonitor:
      enabled: true
      interval: 30s
```

### Install with Helm

```bash
# Add repository (if published)
helm repo add honua https://charts.honua.io
helm repo update

# Install
helm install honua honua/honua \
  --namespace honua \
  --create-namespace \
  --values values-prod.yaml \
  --set postgis.password=<SECURE_PASSWORD>

# Or install from local chart
helm install honua ./honua-chart \
  --namespace honua \
  --create-namespace \
  --values values-prod.yaml

# Upgrade
helm upgrade honua honua/honua \
  --namespace honua \
  --values values-prod.yaml

# Rollback
helm rollback honua 1 --namespace honua

# Uninstall
helm uninstall honua --namespace honua
```

## Storage Configuration

### AWS EBS (Single AZ)

For PostGIS StatefulSet in AWS:

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: fast-ssd
provisioner: ebs.csi.aws.com
parameters:
  type: gp3
  iops: "3000"
  throughput: "125"
  encrypted: "true"
volumeBindingMode: WaitForFirstConsumer
allowVolumeExpansion: true
```

### AWS EFS (Multi-AZ)

For shared tile cache and attachments:

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: efs-sc
provisioner: efs.csi.aws.com
parameters:
  provisioningMode: efs-ap
  fileSystemId: fs-1234567890abcdef0
  directoryPerms: "700"
  gidRangeStart: "1000"
  gidRangeEnd: "2000"
  basePath: "/honua"
```

### Azure Files

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: azure-file
provisioner: file.csi.azure.com
parameters:
  skuName: Premium_LRS
  location: eastus
  resourceGroup: honua-rg
mountOptions:
  - dir_mode=0777
  - file_mode=0777
  - uid=1654
  - gid=1654
  - mfsymlinks
  - cache=strict
  - actimeo=30
```

### GCP Persistent Disk

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: fast-ssd
provisioner: pd.csi.storage.gke.io
parameters:
  type: pd-ssd
  replication-type: regional-pd
volumeBindingMode: WaitForFirstConsumer
allowVolumeExpansion: true
```

## Configuration Management

### ConfigMaps

```bash
# Create from file
kubectl create configmap honua-metadata \
  --from-file=metadata.json \
  --namespace honua

# Create from literal values
kubectl create configmap honua-env \
  --from-literal=HONUA__ODATA__ENABLED=true \
  --from-literal=HONUA__SERVICES__WFS__ENABLED=true \
  --namespace honua

# Create from env file
kubectl create configmap honua-config \
  --from-env-file=honua.env \
  --namespace honua
```

### Secrets

```bash
# Create generic secret
kubectl create secret generic honua-db-secret \
  --from-literal=username=honua \
  --from-literal=password=SecurePassword123! \
  --namespace honua

# Create TLS secret
kubectl create secret tls honua-tls \
  --cert=tls.crt \
  --key=tls.key \
  --namespace honua

# Create from file
kubectl create secret generic honua-oauth \
  --from-file=oauth-config.json \
  --namespace honua
```

### Sealed Secrets (GitOps)

Using Bitnami Sealed Secrets for secure secret storage in Git:

```bash
# Install sealed-secrets controller
helm install sealed-secrets sealed-secrets/sealed-secrets \
  --namespace kube-system

# Create sealed secret
echo -n "SecurePassword123!" | kubectl create secret generic honua-db-password \
  --dry-run=client \
  --from-file=password=/dev/stdin \
  -o yaml | \
  kubeseal -o yaml > honua-db-password-sealed.yaml

# Apply sealed secret
kubectl apply -f honua-db-password-sealed.yaml -n honua
```

## Multi-Region Deployment

### Active-Active Configuration

Deploy across multiple regions with global load balancer:

```yaml
# Region 1 (us-east-1)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua-us-east
  labels:
    region: us-east-1
spec:
  replicas: 3
  # ... (same as above)

---
# Region 2 (eu-west-1)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua-eu-west
  labels:
    region: eu-west-1
spec:
  replicas: 3
  # ... (same as above)
```

**Global Load Balancer** (using AWS Global Accelerator or GCP Cloud Load Balancing):

```yaml
# GCP Multi-cluster Ingress
apiVersion: networking.gke.io/v1
kind: MultiClusterIngress
metadata:
  name: honua-global-ingress
  namespace: honua
spec:
  template:
    spec:
      backend:
        serviceName: honua-server
        servicePort: 80
```

### Database Replication

PostgreSQL primary-replica setup:

```yaml
# Primary (us-east-1)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgis-primary
  namespace: honua-us-east
spec:
  # ... (StatefulSet config)
  template:
    spec:
      containers:
      - name: postgis
        env:
        - name: POSTGRES_REPLICATION_MODE
          value: "master"
        - name: POSTGRES_REPLICATION_USER
          value: "replicator"

---
# Replica (eu-west-1)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgis-replica
  namespace: honua-eu-west
spec:
  # ... (StatefulSet config)
  template:
    spec:
      containers:
      - name: postgis
        env:
        - name: POSTGRES_REPLICATION_MODE
          value: "slave"
        - name: POSTGRES_MASTER_SERVICE_HOST
          value: "postgis-primary.honua-us-east.svc.cluster.local"
```

## Monitoring and Observability

### Prometheus ServiceMonitor

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: honua-server
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
    scrapeTimeout: 10s
```

### Grafana Dashboard

Deploy Grafana with Honua dashboard:

```bash
# Add Grafana Helm repo
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update

# Install Grafana
helm install grafana grafana/grafana \
  --namespace monitoring \
  --create-namespace \
  --set adminPassword=admin \
  --set persistence.enabled=true \
  --set persistence.size=10Gi

# Import Honua dashboard
kubectl create configmap honua-dashboard \
  --from-file=honua-dashboard.json \
  --namespace monitoring
```

### Logging with Fluent Bit

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: fluent-bit-config
  namespace: logging
data:
  fluent-bit.conf: |
    [SERVICE]
        Flush        5
        Daemon       Off
        Log_Level    info

    [INPUT]
        Name              tail
        Path              /var/log/containers/honua-*.log
        Parser            docker
        Tag               kube.*
        Refresh_Interval  5

    [OUTPUT]
        Name  es
        Match kube.*
        Host  elasticsearch
        Port  9200
        Index honua-logs
```

## Security Best Practices

### Pod Security Standards

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: honua-prod
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
```

### RBAC Configuration

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: honua-server
  namespace: honua-prod

---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: honua-server-role
  namespace: honua-prod
rules:
- apiGroups: [""]
  resources: ["configmaps", "secrets"]
  verbs: ["get", "list"]

---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: honua-server-rolebinding
  namespace: honua-prod
subjects:
- kind: ServiceAccount
  name: honua-server
  namespace: honua-prod
roleRef:
  kind: Role
  name: honua-server-role
  apiGroup: rbac.authorization.k8s.io
```

Use the ServiceAccount in deployment:

```yaml
spec:
  template:
    spec:
      serviceAccountName: honua-server
```

### Network Policies

```yaml
# Deny all ingress by default
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-ingress
  namespace: honua-prod
spec:
  podSelector: {}
  policyTypes:
  - Ingress

---
# Allow specific ingress to Honua
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-honua-ingress
  namespace: honua-prod
spec:
  podSelector:
    matchLabels:
      app: honua-server
  policyTypes:
  - Ingress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
```

## Troubleshooting

### Pod Not Starting

```bash
# Describe pod
kubectl describe pod <pod-name> -n honua

# View logs
kubectl logs <pod-name> -n honua

# View previous container logs (if crashed)
kubectl logs <pod-name> -n honua --previous

# Execute shell (if available)
kubectl exec -it <pod-name> -n honua -- /bin/sh
```

### Service Not Accessible

```bash
# Check service endpoints
kubectl get endpoints honua-server -n honua

# Test service from within cluster
kubectl run -it --rm debug --image=curlimages/curl --restart=Never -- \
  curl http://honua-server.honua.svc.cluster.local/health

# Check ingress
kubectl describe ingress honua-ingress -n honua
```

### Database Connection Issues

```bash
# Test PostgreSQL connectivity
kubectl run -it --rm psql --image=postgres:latest --restart=Never -- \
  psql -h postgis-0.postgis.honua.svc.cluster.local -U honua -d honuadb

# Check PostgreSQL logs
kubectl logs postgis-0 -n honua

# Verify secret
kubectl get secret honua-db-secret -n honua -o yaml
```

### Resource Issues

```bash
# Check resource usage
kubectl top pods -n honua
kubectl top nodes

# Check HPA status
kubectl get hpa -n honua
kubectl describe hpa honua-server-hpa -n honua

# Check events
kubectl get events -n honua --sort-by='.lastTimestamp'
```

## CI/CD Integration

### GitHub Actions

```yaml
name: Deploy to Kubernetes

on:
  push:
    branches: [main]
    tags: ['v*']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: us-east-1

      - name: Update kubeconfig
        run: |
          aws eks update-kubeconfig --name honua-cluster --region us-east-1

      - name: Deploy to Kubernetes
        run: |
          kubectl apply -f k8s/production/ -n honua-prod
          kubectl rollout status deployment/honua-server -n honua-prod
```

### ArgoCD (GitOps)

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: honua
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/your-org/honua-config
    targetRevision: HEAD
    path: k8s/production
  destination:
    server: https://kubernetes.default.svc
    namespace: honua-prod
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
    - CreateNamespace=true
```

## See Also

- [Docker Deployment](docker-deployment.md) - Container deployment guide
- [AWS ECS Deployment](aws-ecs-deployment.md) - AWS container orchestration
- [Environment Variables](../01-configuration/environment-variables.md) - Configuration reference
- [Monitoring and Observability](../04-operations/monitoring-observability.md) - Production monitoring
- [Security Best Practices](../04-operations/security-best-practices.md) - Security hardening
- [Performance Tuning](../04-operations/performance-tuning.md) - Optimization guide
