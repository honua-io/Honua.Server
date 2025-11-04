// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for generating Kubernetes manifest configurations.
/// </summary>
public sealed class KubernetesConfigurationService
{
    private readonly ILogger<KubernetesConfigurationService> _logger;

    public KubernetesConfigurationService(ILogger<KubernetesConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates Kubernetes manifests based on deployment analysis.
    /// </summary>
    public async Task<string> GenerateAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        var k8sDir = context.WorkspacePath;
        Directory.CreateDirectory(k8sDir);

        var manifestFiles = new StringBuilder();

        // Generate namespace manifest
        var namespaceContent = GenerateNamespace(analysis);
        var namespacePath = Path.Combine(k8sDir, "00-namespace.yaml");
        await File.WriteAllTextAsync(namespacePath, namespaceContent.Trim());
        manifestFiles.AppendLine($"Generated 00-namespace.yaml");

        // Generate deployment manifest for Honua server
        var deploymentContent = GenerateDeployment(analysis);
        var deploymentPath = Path.Combine(k8sDir, "01-deployment.yaml");
        await File.WriteAllTextAsync(deploymentPath, deploymentContent.Trim());
        manifestFiles.AppendLine($"Generated 01-deployment.yaml");

        // Generate service manifest
        var serviceContent = GenerateService(analysis);
        var servicePath = Path.Combine(k8sDir, "02-service.yaml");
        await File.WriteAllTextAsync(servicePath, serviceContent.Trim());
        manifestFiles.AppendLine($"Generated 02-service.yaml");

        // Generate ConfigMap if needed
        if (analysis.RequiredServices.Count > 1)
        {
            var configMapContent = GenerateConfigMap(analysis);
            var configMapPath = Path.Combine(k8sDir, "03-configmap.yaml");
            await File.WriteAllTextAsync(configMapPath, configMapContent.Trim());
            manifestFiles.AppendLine($"Generated 03-configmap.yaml");
        }

        // Generate database resources if needed
        if (analysis.InfrastructureNeeds.NeedsDatabase)
        {
            var dbContent = GenerateDatabase(analysis);
            var dbPath = Path.Combine(k8sDir, "04-database.yaml");
            await File.WriteAllTextAsync(dbPath, dbContent.Trim());
            manifestFiles.AppendLine($"Generated 04-database.yaml");
        }

        // Generate Redis resources if needed
        if (analysis.InfrastructureNeeds.NeedsCache)
        {
            var redisContent = GenerateRedis(analysis);
            var redisPath = Path.Combine(k8sDir, "05-redis.yaml");
            await File.WriteAllTextAsync(redisPath, redisContent.Trim());
            manifestFiles.AppendLine($"Generated 05-redis.yaml");
        }

        return manifestFiles.ToString();
    }

    private string GenerateNamespace(DeploymentAnalysis analysis)
    {
        return $@"apiVersion: v1
kind: Namespace
metadata:
  name: honua
  labels:
    environment: {analysis.TargetEnvironment}";
    }

    private string GenerateDeployment(DeploymentAnalysis analysis)
    {
        var hasDatabase = analysis.InfrastructureNeeds.NeedsDatabase ||
                         analysis.RequiredServices.Any(s => s.Contains("postgis", StringComparison.OrdinalIgnoreCase));
        var hasCache = analysis.InfrastructureNeeds.NeedsCache ||
                      analysis.RequiredServices.Any(s => s.Contains("redis", StringComparison.OrdinalIgnoreCase));

        return $@"apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua
  labels:
    app: honua-server
    environment: {analysis.TargetEnvironment}
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
        environment: {analysis.TargetEnvironment}
    spec:
      containers:
      - name: honua-server
        image: honuaio/honua-server:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: {analysis.TargetEnvironment}
{(hasDatabase ? @"        - name: HONUA__DATABASE__HOST
          value: postgis-service
        - name: HONUA__DATABASE__PORT
          value: ""5432""
        - name: HONUA__DATABASE__DATABASE
          value: honua
        - name: HONUA__DATABASE__USERNAME
          valueFrom:
            secretKeyRef:
              name: database-secret
              key: username
        - name: HONUA__DATABASE__PASSWORD
          valueFrom:
            secretKeyRef:
              name: database-secret
              key: password" : "")}
{(hasCache ? @"        - name: HONUA__CACHE__PROVIDER
          value: redis
        - name: HONUA__CACHE__REDIS__HOST
          value: redis-service
        - name: HONUA__CACHE__REDIS__PORT
          value: ""6379""" : "")}
        resources:
          requests:
            memory: ""512Mi""
            cpu: ""500m""
          limits:
            memory: ""1Gi""
            cpu: ""1000m""
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5";
    }

    private string GenerateService(DeploymentAnalysis analysis)
    {
        var needsLoadBalancer = analysis.InfrastructureNeeds.NeedsLoadBalancer ||
                               analysis.TargetEnvironment == "production";

        return $@"apiVersion: v1
kind: Service
metadata:
  name: honua-service
  namespace: honua
  labels:
    app: honua-server
    environment: {analysis.TargetEnvironment}
spec:
  type: {(needsLoadBalancer ? "LoadBalancer" : "ClusterIP")}
  selector:
    app: honua-server
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP";
    }

    private string GenerateConfigMap(DeploymentAnalysis analysis)
    {
        return $@"apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua
data:
  environment: {analysis.TargetEnvironment}
  services: {string.Join(",", analysis.RequiredServices)}";
    }

    private string GenerateDatabase(DeploymentAnalysis analysis)
    {
        return @"apiVersion: v1
kind: Secret
metadata:
  name: database-secret
  namespace: honua
type: Opaque
stringData:
  username: postgres
  password: changeme123
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgis
  namespace: honua
spec:
  serviceName: postgis-service
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
        ports:
        - containerPort: 5432
        env:
        - name: POSTGRES_DB
          value: honua
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: database-secret
              key: username
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: database-secret
              key: password
        volumeMounts:
        - name: postgis-storage
          mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
  - metadata:
      name: postgis-storage
    spec:
      accessModes: [""ReadWriteOnce""]
      resources:
        requests:
          storage: 10Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgis-service
  namespace: honua
spec:
  selector:
    app: postgis
  ports:
  - port: 5432
    targetPort: 5432";
    }

    private string GenerateRedis(DeploymentAnalysis analysis)
    {
        return @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
  namespace: honua
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:7-alpine
        ports:
        - containerPort: 6379
        resources:
          requests:
            memory: ""256Mi""
            cpu: ""100m""
          limits:
            memory: ""512Mi""
            cpu: ""200m""
---
apiVersion: v1
kind: Service
metadata:
  name: redis-service
  namespace: honua
spec:
  selector:
    app: redis
  ports:
  - port: 6379
    targetPort: 6379";
    }
}
