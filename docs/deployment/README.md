# Honua Deployment Guide

Production deployment guide for Honua across different platforms.

## Docker (Recommended for Development)

### Basic Deployment

```bash
cd docker
docker compose up --build
```

### Production Docker Deployment

```yaml
# docker-compose.prod.yml
version: "3.9"

services:
  db:
    image: postgres:16
    environment:
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: ${DB_NAME}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    restart: unless-stopped

  web:
    image: honua:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: ${CONNECTION_STRING}
      honua__authentication__mode: OIDC
      honua__observability__metrics__enabled: "true"
      honua__observability__tracing__exporter: otlp
      honua__observability__tracing__otlpEndpoint: http://jaeger:4317
    ports:
      - "8080:8080"
    restart: unless-stopped
    depends_on:
      - db

volumes:
  postgres-data:
```

## Kubernetes

### Basic Deployment

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: honua

---
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua
data:
  appsettings.json: |
    {
      "observability": {
        "metrics": {
          "enabled": true
        }
      }
    }

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua
  namespace: honua
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua
  template:
    metadata:
      labels:
        app: honua
    spec:
      containers:
      - name: honua
        image: honua:latest
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: honua-db
              key: connection-string
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.json
          subPath: appsettings.json
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
      volumes:
      - name: config
        configMap:
          name: honua-config

---
apiVersion: v1
kind: Service
metadata:
  name: honua
  namespace: honua
spec:
  selector:
    app: honua
  ports:
  - port: 80
    targetPort: 8080
  type: ClusterIP

---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua
  namespace: honua
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - gis.example.com
    secretName: honua-tls
  rules:
  - host: gis.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: honua
            port:
              number: 80
```

### Horizontal Pod Autoscaler

Honua includes production-ready HPA manifests in `deploy/kubernetes/production/07-hpa.yaml`.

#### Quick Deployment

```bash
# Apply HPA configuration
kubectl apply -f deploy/kubernetes/production/07-hpa.yaml

# Verify HPA is working
kubectl get hpa -n honua
kubectl describe hpa honua-server -n honua

# Monitor scaling in real-time
kubectl get hpa -n honua -w
```

#### Configuration Overview

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-server
  namespace: honua
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-server

  # Replica limits
  minReplicas: 2
  maxReplicas: 10

  # Scaling metrics
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70  # Scale when CPU > 70%

  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80  # Scale when memory > 80%

  # Scaling behavior
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300  # Wait 5 min before scaling down
      policies:
      - type: Percent
        value: 50              # Max 50% reduction per minute
        periodSeconds: 60

    scaleUp:
      stabilizationWindowSeconds: 0    # Scale up immediately
      policies:
      - type: Percent
        value: 100             # Max 100% increase per 30 seconds
        periodSeconds: 30
      - type: Pods
        value: 4               # Or add max 4 pods per 30 seconds
        periodSeconds: 30
      selectPolicy: Max        # Use most aggressive policy
```

#### Scaling Thresholds

| Metric | Target | Min Replicas | Max Replicas | Scale Up Policy | Scale Down Policy |
|--------|--------|--------------|--------------|-----------------|-------------------|
| CPU | 70% | 2 | 10 | 100% or +4 pods/30s | 50%/60s, max -2 pods |
| Memory | 80% | 2 | 10 | 100% or +4 pods/30s | 50%/60s, max -2 pods |

#### Prerequisites

The Kubernetes Metrics Server must be installed for HPA to work:

```bash
# Check if metrics server is installed
kubectl get deployment metrics-server -n kube-system

# Install metrics server if needed
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Verify metrics are available
kubectl top nodes
kubectl top pods -n honua
```

#### Monitoring Autoscaling

```bash
# Watch HPA status
kubectl get hpa -n honua -w

# View HPA events
kubectl describe hpa honua-server -n honua

# Check current pod count
kubectl get pods -n honua -l app=honua-server

# View scaling history
kubectl get events -n honua --field-selector involvedObject.name=honua-server
```

#### Custom Metrics (Optional)

For advanced scaling based on application-specific metrics, uncomment the custom metrics section in the HPA manifest and install Prometheus Adapter:

```bash
# Install Prometheus Adapter
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus-adapter prometheus-community/prometheus-adapter \
  --namespace monitoring \
  --create-namespace

# Configure custom metrics (examples in hpa.yaml):
# - http_requests_per_second
# - tile_processing_queue_length
# - database_connection_pool_utilization
```

#### Troubleshooting

**HPA shows "unknown" for metrics:**
```bash
# Check metrics server logs
kubectl logs -n kube-system deployment/metrics-server

# Verify pod resource requests are set
kubectl get deployment honua-server -n honua -o yaml | grep -A 4 resources
```

**Pods not scaling:**
```bash
# Check HPA conditions
kubectl describe hpa honua-server -n honua

# Verify metrics are available
kubectl top pods -n honua

# Check for PodDisruptionBudget conflicts
kubectl get pdb -n honua
```

**Rapid scaling (flapping):**
- Increase `stabilizationWindowSeconds` for scaleDown
- Adjust metric thresholds (CPU/memory targets)
- Review application resource requests/limits

## AWS

### ECS Fargate

```json
{
  "family": "honua",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "1024",
  "memory": "2048",
  "containerDefinitions": [
    {
      "name": "honua",
      "image": "honua:latest",
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__DefaultConnection",
          "valueFrom": "arn:aws:secretsmanager:region:account:secret:honua-db"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/honua",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "honua"
        }
      },
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost:8080/health/live || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3
      }
    }
  ]
}
```

## Azure

### Azure Container Apps

```bash
az containerapp create \
  --name honua \
  --resource-group honua-rg \
  --environment honua-env \
  --image honua:latest \
  --target-port 8080 \
  --ingress external \
  --min-replicas 2 \
  --max-replicas 10 \
  --cpu 1.0 \
  --memory 2Gi \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    honua__observability__metrics__enabled=true \
  --secrets \
    connection-string="${CONNECTION_STRING}"
```

## Google Cloud

### Cloud Run

```bash
gcloud run deploy honua \
  --image honua:latest \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --port 8080 \
  --min-instances 2 \
  --max-instances 10 \
  --cpu 1 \
  --memory 2Gi \
  --set-env-vars ASPNETCORE_ENVIRONMENT=Production \
  --set-secrets ConnectionStrings__DefaultConnection=honua-db:latest
```

## Production Checklist

- [ ] Change default passwords
- [ ] Enable authentication (OIDC or Local mode)
- [ ] Configure TLS/HTTPS certificates
- [ ] Set up rate limiting
- [ ] Configure observability (metrics, traces, logs)
- [ ] Set up backup and disaster recovery
- [ ] Configure secrets management
- [ ] Enable CORS policies
- [ ] Set up monitoring and alerting
- [ ] Configure autoscaling
- [ ] Test health check endpoints
- [ ] Review security settings

## Performance Tuning

### Connection Pooling

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db;Database=honua;Username=user;Password=pass;Pooling=true;MinPoolSize=10;MaxPoolSize=100"
  }
}
```

### Caching

```json
{
  "honua": {
    "caching": {
      "metadata": {
        "enabled": true,
        "ttlMinutes": 60
      },
      "raster": {
        "enabled": true,
        "maxSizeMb": 10240
      }
    }
  }
}
```

### Rate Limiting

```json
{
  "RateLimiting": {
    "Enabled": true,
    "Default": {
      "PermitLimit": 100,
      "WindowMinutes": 1
    }
  }
}
```

## Monitoring

See [Observability Documentation](../observability/) for detailed monitoring setup.

---

For platform-specific guides and troubleshooting, see archived documentation in `docs/archive/2025-10-15/`.
