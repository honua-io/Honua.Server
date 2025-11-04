// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Documentation;

/// <summary>
/// Service for creating deployment guides for various platforms.
/// </summary>
public sealed class DeploymentGuideService
{
    /// <summary>
    /// Creates deployment guides for Docker, Kubernetes, and Azure.
    /// </summary>
    /// <param name="infrastructure">Infrastructure details as JSON</param>
    /// <returns>JSON containing deployment guides for multiple platforms</returns>
    public string CreateDeploymentGuide(string infrastructure = "{\"platform\":\"docker\",\"environment\":\"production\"}")
    {
        var deploymentGuides = new
        {
            dockerDeployment = @"# Docker Deployment Guide

## Prerequisites
- Docker 20.10+
- Docker Compose 2.0+

## Quick Start

1. **Clone Repository**
```bash
git clone https://github.com/your-org/honua.git
cd honua
```

2. **Configure Environment**
```bash
cp .env.example .env
# Edit .env with your settings
```

3. **Start Services**
```bash
docker-compose up -d
```

4. **Verify Deployment**
```bash
curl http://localhost:5000/
```

## Docker Compose Configuration

```yaml
version: '3.8'

services:
  postgis:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_DB: honua
      POSTGRES_USER: honua_user
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgis_data:/var/lib/postgresql/data
    ports:
      - ""5432:5432""

  honua:
    image: honua/server:latest
    depends_on:
      - postgis
    environment:
      ConnectionStrings__DefaultConnection: ""Host=postgis;Database=honua;Username=honua_user;Password=${DB_PASSWORD}""
      HONUA__METADATA__PROVIDER: yaml
      HONUA__METADATA__PATH: /app/metadata.yaml
    volumes:
      - ./metadata.yaml:/app/metadata.yaml:ro
    ports:
      - ""5000:5000""

  nginx:
    image: nginx:alpine
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    ports:
      - ""80:80""
      - ""443:443""
    depends_on:
      - honua

volumes:
  postgis_data:
```

## Production Optimizations

```dockerfile
# Optimized Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY [""Honua.Server.Host/Honua.Server.Host.csproj"", ""Honua.Server.Host/""]
RUN dotnet restore ""Honua.Server.Host/Honua.Server.Host.csproj""
COPY . .
WORKDIR ""/src/Honua.Server.Host""
RUN dotnet build ""Honua.Server.Host.csproj"" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ""Honua.Server.Host.csproj"" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""Honua.Server.Host.dll""]
```
",

            kubernetesDeployment = @"# Kubernetes Deployment Guide

## Prerequisites
- Kubernetes 1.24+
- kubectl configured
- Helm 3.0+ (optional)

## Deployment Steps

1. **Create Namespace**
```bash
kubectl create namespace honua
```

2. **Deploy PostgreSQL**
```bash
kubectl apply -f kubernetes/postgis-deployment.yaml
```

3. **Create ConfigMap**
```bash
kubectl create configmap honua-metadata --from-file=metadata.yaml -n honua
```

4. **Deploy Honua**
```bash
kubectl apply -f kubernetes/honua-deployment.yaml
```

## Kubernetes Manifests

### postgis-deployment.yaml
```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgis-pvc
  namespace: honua
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 50Gi
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgis
  namespace: honua
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgis
  template:
    metadata:
      labels:
        app: postgis
    spec:
      containers:
      - name: postgis
        image: postgis/postgis:16-3.4
        env:
        - name: POSTGRES_DB
          value: ""honua""
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: postgis-secret
              key: username
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgis-secret
              key: password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgis-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgis-storage
        persistentVolumeClaim:
          claimName: postgis-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgis
  namespace: honua
spec:
  ports:
  - port: 5432
  selector:
    app: postgis
```

### honua-deployment.yaml
```yaml
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
        image: honua/server:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: honua-secret
              key: connection-string
        - name: HONUA__METADATA__PROVIDER
          value: ""yaml""
        - name: HONUA__METADATA__PATH
          value: ""/app/metadata/metadata.yaml""
        ports:
        - containerPort: 5000
        volumeMounts:
        - name: metadata
          mountPath: /app/metadata
        livenessProbe:
          httpGet:
            path: /health/live
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            memory: ""256Mi""
            cpu: ""250m""
          limits:
            memory: ""512Mi""
            cpu: ""500m""
      volumes:
      - name: metadata
        configMap:
          name: honua-metadata
---
apiVersion: v1
kind: Service
metadata:
  name: honua
  namespace: honua
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
  selector:
    app: honua
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-hpa
  namespace: honua
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```
",

            azureDeployment = @"# Azure App Service Deployment

## Prerequisites
- Azure CLI installed
- Azure subscription

## Deployment Steps

1. **Create Resource Group**
```bash
az group create --name honua-rg --location eastus
```

2. **Create PostgreSQL Server**
```bash
az postgres flexible-server create \
    --resource-group honua-rg \
    --name honua-db \
    --location eastus \
    --admin-user honuaadmin \
    --admin-password <password> \
    --sku-name Standard_D2s_v3 \
    --version 14 \
    --storage-size 128

# Enable PostGIS extension
az postgres flexible-server parameter set \
    --resource-group honua-rg \
    --server-name honua-db \
    --name azure.extensions \
    --value postgis
```

3. **Create App Service**
```bash
az appservice plan create \
    --name honua-plan \
    --resource-group honua-rg \
    --sku P1V2 \
    --is-linux

az webapp create \
    --resource-group honua-rg \
    --plan honua-plan \
    --name honua-api \
    --deployment-container-image-name honua/server:latest
```

4. **Configure App Settings**
```bash
az webapp config appsettings set \
    --resource-group honua-rg \
    --name honua-api \
    --settings \
        ConnectionStrings__DefaultConnection=""Host=honua-db.postgres.database.azure.com;Database=honua;Username=honuaadmin;Password=<password>;SSL Mode=Require"" \
        HONUA__METADATA__PROVIDER=yaml \
        HONUA__METADATA__PATH=/app/metadata.yaml
```

5. **Enable Managed Identity and Monitoring**
```bash
# Enable managed identity
az webapp identity assign --resource-group honua-rg --name honua-api

# Enable Application Insights
az monitor app-insights component create \
    --app honua-insights \
    --location eastus \
    --resource-group honua-rg
```
"
        };

        return JsonSerializer.Serialize(deploymentGuides, CliJsonOptions.Indented);
    }
}
