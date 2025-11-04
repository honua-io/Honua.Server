// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Generates observability and telemetry configurations for deployed Honua infrastructure.
/// Creates configurations for OpenTelemetry exporters, Prometheus, Grafana, and alerting.
/// </summary>
public sealed class ObservabilityConfigurationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ObservabilityConfigurationAgent> _logger;

    public ObservabilityConfigurationAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<ObservabilityConfigurationAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates complete observability stack configuration.
    /// </summary>
    public async Task<AgentStepResult> GenerateObservabilityConfigAsync(
        ObservabilityConfigRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating observability configuration for {Deployment}", request.DeploymentName);

        var configurations = new Dictionary<string, string>();

        try
        {
            // 1. OpenTelemetry Collector configuration
            var otelConfig = await GenerateOTelCollectorConfigAsync(request, context, cancellationToken);
            configurations["otel-collector.yaml"] = otelConfig;

            // 2. Prometheus configuration with Honua-specific metrics
            var prometheusConfig = await GeneratePrometheusConfigAsync(request, context, cancellationToken);
            configurations["prometheus.yml"] = prometheusConfig;

            // 3. Grafana dashboards for Honua metrics
            var grafanaDashboards = await GenerateGrafanaDashboardsAsync(request, context, cancellationToken);
            configurations["grafana-dashboard-honua.json"] = grafanaDashboards;

            // 4. Alerting rules for Honua health
            var alertingRules = await GenerateAlertingRulesAsync(request, context, cancellationToken);
            configurations["alert-rules.yml"] = alertingRules;

            // 5. Docker Compose / Kubernetes manifests for observability stack
            if (request.Platform == "docker-compose")
            {
                var dockerCompose = GenerateDockerComposeObservability(request);
                configurations["docker-compose.observability.yml"] = dockerCompose;
            }
            else if (request.Platform == "kubernetes")
            {
                var k8sManifests = GenerateKubernetesObservability(request);
                configurations["observability-stack.yaml"] = k8sManifests;
            }

            // 6. Health check endpoints configuration
            var healthEndpoints = GenerateHealthCheckEndpoints(request);
            configurations["health-endpoints.json"] = healthEndpoints;

            var summary = new StringBuilder();
            summary.AppendLine($"Generated observability configuration for {request.DeploymentName}:");
            summary.AppendLine();
            summary.AppendLine("📊 **Monitoring Stack**:");
            summary.AppendLine($"  • OpenTelemetry Collector: Configured for {request.Backend} backend");
            summary.AppendLine($"  • Prometheus: Scraping Honua metrics from {request.HonuaMetricsEndpoint}");
            summary.AppendLine("  • Grafana: Dashboard for Honua API metrics (requests, errors, latency)");
            summary.AppendLine($"  • Alerting: {alertingRules.Split('\n').Count(l => l.Contains("alert:"))} alert rules configured");
            summary.AppendLine();
            summary.AppendLine("🔍 **Monitored Metrics**:");
            summary.AppendLine("  • honua.api.requests - Request count by protocol/service/layer");
            summary.AppendLine("  • honua.api.request_duration - Latency percentiles (p50, p95, p99)");
            summary.AppendLine("  • honua.api.errors - Error rate by type and category");
            summary.AppendLine("  • honua.api.features_returned - Feature count per request");
            summary.AppendLine();
            summary.AppendLine("⚠️  **Alert Conditions**:");
            summary.AppendLine("  • Error rate > 5% (Warning)");
            summary.AppendLine("  • Error rate > 10% (Critical)");
            summary.AppendLine("  • P95 latency > 2s (Warning)");
            summary.AppendLine("  • P99 latency > 5s (Critical)");
            summary.AppendLine("  • Service unavailable for 2+ minutes (Critical)");
            summary.AppendLine();
            summary.AppendLine($"Files generated: {configurations.Count}");

            var artifact = JsonSerializer.Serialize(configurations, CliJsonOptions.Indented);
            var message = $"{summary}\n\nGenerated Configuration:\n{artifact}";

            return new AgentStepResult
            {
                AgentName = "ObservabilityConfiguration",
                Action = "generate_observability_config",
                Success = true,
                Message = message,
                Duration = TimeSpan.Zero
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate observability configuration");
            return new AgentStepResult
            {
                AgentName = "ObservabilityConfiguration",
                Action = "generate_observability_config",
                Success = false,
                Message = $"Failed to generate observability configuration: {ex.Message}",
                Duration = TimeSpan.Zero
            };
        }
    }

    private Task<string> GenerateOTelCollectorConfigAsync(
        ObservabilityConfigRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var config = $@"receivers:
  prometheus:
    config:
      scrape_configs:
        - job_name: 'honua-server'
          scrape_interval: 15s
          static_configs:
            - targets: ['{request.HonuaMetricsEndpoint}']

  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 10s
    send_batch_size: 1024

  resource:
    attributes:
      - key: service.name
        value: honua-server
        action: upsert
      - key: deployment.environment
        value: {request.Environment}
        action: upsert

exporters:
  {GenerateOTelExporter(request)}

  prometheus:
    endpoint: 0.0.0.0:8889
    namespace: honua

  logging:
    loglevel: info

service:
  pipelines:
    metrics:
      receivers: [prometheus, otlp]
      processors: [batch, resource]
      exporters: [{request.Backend}, prometheus, logging]

    traces:
      receivers: [otlp]
      processors: [batch, resource]
      exporters: [{request.Backend}, logging]
";

        return Task.FromResult(config);
    }

    private string GenerateOTelExporter(ObservabilityConfigRequest request)
    {
        return request.Backend.ToLowerInvariant() switch
        {
            "jaeger" => @"jaeger:
    endpoint: jaeger:14250
    tls:
      insecure: true",

            "zipkin" => @"zipkin:
    endpoint: http://zipkin:9411/api/v2/spans",

            "otlp" => @"otlp:
    endpoint: otel-collector:4317
    tls:
      insecure: true",

            "datadog" => @"datadog:
    api:
      key: ${env:DD_API_KEY}
      site: datadoghq.com",

            "newrelic" => @"otlphttp:
    endpoint: https://otlp.nr-data.net
    headers:
      api-key: ${env:NEW_RELIC_LICENSE_KEY}",

            _ => @"otlp:
    endpoint: localhost:4317
    tls:
      insecure: true"
        };
    }

    private Task<string> GeneratePrometheusConfigAsync(
        ObservabilityConfigRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult($@"global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    environment: '{request.Environment}'
    deployment: '{request.DeploymentName}'

rule_files:
  - 'alert-rules.yml'

alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']

scrape_configs:
  - job_name: 'honua-server'
    scrape_interval: 10s
    static_configs:
      - targets: ['{request.HonuaMetricsEndpoint}']
    metric_relabel_configs:
      # Keep only Honua-specific metrics
      - source_labels: [__name__]
        regex: 'honua_.*'
        action: keep

  - job_name: 'honua-database'
    scrape_interval: 30s
    static_configs:
      - targets: ['{request.DatabaseMetricsEndpoint ?? "postgres-exporter:9187"}']

  - job_name: 'otel-collector'
    scrape_interval: 10s
    static_configs:
      - targets: ['otel-collector:8889']
");
    }

    private Task<string> GenerateGrafanaDashboardsAsync(
        ObservabilityConfigRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Generate comprehensive Grafana dashboard for Honua metrics
        var dashboard = new
        {
            title = "Honua Server - API Metrics",
            uid = "honua-api-metrics",
            tags = new[] { "honua", "geospatial", "ogc" },
            timezone = "browser",
            panels = new[]
            {
                new
                {
                    title = "Request Rate (by Protocol)",
                    type = "graph",
                    targets = new[] {
                        new {
                            expr = "rate(honua_api_requests_total[5m])",
                            legendFormat = "{{api_protocol}}"
                        }
                    }
                },
                new
                {
                    title = "Error Rate",
                    type = "graph",
                    targets = new[] {
                        new {
                            expr = "rate(honua_api_errors_total[5m])",
                            legendFormat = "{{error_type}}"
                        }
                    }
                },
                new
                {
                    title = "Request Duration (P95, P99)",
                    type = "graph",
                    targets = new[] {
                        new {
                            expr = "histogram_quantile(0.95, rate(honua_api_request_duration_bucket[5m]))",
                            legendFormat = "p95"
                        },
                        new {
                            expr = "histogram_quantile(0.99, rate(honua_api_request_duration_bucket[5m]))",
                            legendFormat = "p99"
                        }
                    }
                },
                new
                {
                    title = "Features Returned (by Layer)",
                    type = "graph",
                    targets = new[] {
                        new {
                            expr = "rate(honua_api_features_returned_total[5m])",
                            legendFormat = "{{layer_id}}"
                        }
                    }
                }
            }
        };

        return Task.FromResult(JsonSerializer.Serialize(dashboard, CliJsonOptions.Indented));
    }

    private Task<string> GenerateAlertingRulesAsync(
        ObservabilityConfigRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult($@"groups:
  - name: honua_api_alerts
    interval: 30s
    rules:
      - alert: HonuaHighErrorRate
        expr: |
          rate(honua_api_errors_total[5m]) / rate(honua_api_requests_total[5m]) > 0.05
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: ""High error rate on Honua API""
          description: ""Error rate is {{{{$value | humanizePercentage}}}} (threshold: 5%)""

      - alert: HonuaCriticalErrorRate
        expr: |
          rate(honua_api_errors_total[5m]) / rate(honua_api_requests_total[5m]) > 0.10
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: ""Critical error rate on Honua API""
          description: ""Error rate is {{{{$value | humanizePercentage}}}} (threshold: 10%). ROLLBACK RECOMMENDED.""

      - alert: HonuaSlowResponse
        expr: |
          histogram_quantile(0.95, rate(honua_api_request_duration_bucket[5m])) > 2000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: ""Slow API response times""
          description: ""P95 latency is {{{{$value}}}}ms (threshold: 2000ms)""

      - alert: HonuaCriticalLatency
        expr: |
          histogram_quantile(0.99, rate(honua_api_request_duration_bucket[5m])) > 5000
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: ""Critical API latency""
          description: ""P99 latency is {{{{$value}}}}ms (threshold: 5000ms)""

      - alert: HonuaServiceDown
        expr: |
          up{{job=""honua-server""}} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: ""Honua service is down""
          description: ""Honua server has been unavailable for 2+ minutes. IMMEDIATE ACTION REQUIRED.""

      - alert: HonuaDatabaseErrors
        expr: |
          rate(honua_api_errors_total{{error_category=""database""}}[5m]) > 1
        for: 3m
        labels:
          severity: high
        annotations:
          summary: ""Database errors detected""
          description: ""{{{{$value}}}} database errors per second. Check PostGIS connection and queries.""
");
    }

    private string GenerateDockerComposeObservability(ObservabilityConfigRequest request)
    {
        return $@"version: '3.8'

services:
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: [""--config=/etc/otel-collector-config.yaml""]
    volumes:
      - ./otel-collector.yaml:/etc/otel-collector-config.yaml
    ports:
      - ""4317:4317""   # OTLP gRPC
      - ""4318:4318""   # OTLP HTTP
      - ""8889:8889""   # Prometheus metrics
    networks:
      - observability

  prometheus:
    image: prom/prometheus:latest
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - ./alert-rules.yml:/etc/prometheus/alert-rules.yml
      - prometheus-data:/prometheus
    ports:
      - ""9090:9090""
    networks:
      - observability

  grafana:
    image: grafana/grafana:latest
    volumes:
      - ./grafana-dashboard-honua.json:/etc/grafana/provisioning/dashboards/honua.json
      - grafana-data:/var/lib/grafana
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    ports:
      - ""3000:3000""
    networks:
      - observability

  alertmanager:
    image: prom/alertmanager:latest
    volumes:
      - ./alertmanager.yml:/etc/alertmanager/alertmanager.yml
    ports:
      - ""9093:9093""
    networks:
      - observability

networks:
  observability:
    driver: bridge

volumes:
  prometheus-data:
  grafana-data:
";
    }

    private string GenerateKubernetesObservability(ObservabilityConfigRequest request)
    {
        return $@"apiVersion: v1
kind: Namespace
metadata:
  name: observability
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: otel-collector
  namespace: observability
spec:
  replicas: 2
  selector:
    matchLabels:
      app: otel-collector
  template:
    metadata:
      labels:
        app: otel-collector
    spec:
      containers:
      - name: otel-collector
        image: otel/opentelemetry-collector-contrib:latest
        args: [""--config=/etc/otel-collector-config.yaml""]
        ports:
        - containerPort: 4317
          name: otlp-grpc
        - containerPort: 4318
          name: otlp-http
        - containerPort: 8889
          name: prometheus
        volumeMounts:
        - name: config
          mountPath: /etc/otel-collector-config.yaml
          subPath: otel-collector.yaml
      volumes:
      - name: config
        configMap:
          name: otel-collector-config
---
apiVersion: v1
kind: Service
metadata:
  name: otel-collector
  namespace: observability
spec:
  selector:
    app: otel-collector
  ports:
  - name: otlp-grpc
    port: 4317
  - name: otlp-http
    port: 4318
  - name: prometheus
    port: 8889
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prometheus
  namespace: observability
spec:
  replicas: 1
  selector:
    matchLabels:
      app: prometheus
  template:
    metadata:
      labels:
        app: prometheus
    spec:
      containers:
      - name: prometheus
        image: prom/prometheus:latest
        args:
        - '--config.file=/etc/prometheus/prometheus.yml'
        - '--storage.tsdb.path=/prometheus'
        ports:
        - containerPort: 9090
        volumeMounts:
        - name: config
          mountPath: /etc/prometheus
        - name: storage
          mountPath: /prometheus
      volumes:
      - name: config
        configMap:
          name: prometheus-config
      - name: storage
        emptyDir: {{}}
---
apiVersion: v1
kind: Service
metadata:
  name: prometheus
  namespace: observability
spec:
  selector:
    app: prometheus
  ports:
  - port: 9090
";
    }

    private string GenerateHealthCheckEndpoints(ObservabilityConfigRequest request)
    {
        var endpoints = new
        {
            health_endpoints = new[]
            {
                new
                {
                    name = "Honua API Health",
                    url = $"{request.HonuaBaseUrl}/health",
                    expected_status = 200,
                    timeout_ms = 5000
                },
                new
                {
                    name = "Honua Metrics",
                    url = $"{request.HonuaBaseUrl}/metrics",
                    expected_status = 200,
                    timeout_ms = 3000
                },
                new
                {
                    name = "OGC API Landing Page",
                    url = $"{request.HonuaBaseUrl}/",
                    expected_status = 200,
                    timeout_ms = 5000
                },
                new
                {
                    name = "Prometheus",
                    url = "http://prometheus:9090/-/healthy",
                    expected_status = 200,
                    timeout_ms = 3000
                },
                new
                {
                    name = "Grafana",
                    url = "http://grafana:3000/api/health",
                    expected_status = 200,
                    timeout_ms = 3000
                }
            }
        };

        return JsonSerializer.Serialize(endpoints, CliJsonOptions.Indented);
    }
}

public sealed class ObservabilityConfigRequest
{
    public string DeploymentName { get; init; } = string.Empty;
    public string Environment { get; init; } = "production";
    public string Platform { get; init; } = "docker-compose"; // docker-compose | kubernetes | terraform
    public string Backend { get; init; } = "prometheus"; // prometheus | jaeger | datadog | newrelic
    public string HonuaBaseUrl { get; init; } = "http://honua:8080";
    public string HonuaMetricsEndpoint { get; init; } = "honua:8080/metrics";
    public string? DatabaseMetricsEndpoint { get; init; }
}
