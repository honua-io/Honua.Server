// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for observability and monitoring.
/// Provides AI with capabilities for metrics, logging strategies, and performance monitoring.
/// </summary>
public sealed class MonitoringPlugin
{
    [KernelFunction, Description("Suggests which metrics to collect based on service profile")]
    public string SuggestMetrics(
        [Description("Service profile as JSON (type, scale, criticality)")] string serviceProfile = "{\"type\":\"api\",\"scale\":\"medium\",\"criticality\":\"high\"}")
    {
        var metrics = new
        {
            coreMetrics = new[]
            {
                new
                {
                    category = "Request Metrics",
                    metrics = new[]
                    {
                        new { name = "http_requests_total", type = "Counter", description = "Total HTTP requests by method, endpoint, status", labels = (string[]?)new[] { "method", "endpoint", "status_code" }, buckets = (double[]?)null },
                        new { name = "http_request_duration_seconds", type = "Histogram", description = "Request duration in seconds", labels = (string[]?)null, buckets = (double[]?)new[] { 0.01, 0.05, 0.1, 0.5, 1.0, 5.0 } },
                        new { name = "http_requests_in_flight", type = "Gauge", description = "Current in-flight requests", labels = (string[]?)null, buckets = (double[]?)null },
                        new { name = "http_response_size_bytes", type = "Histogram", description = "Response body size distribution", labels = (string[]?)null, buckets = (double[]?)null }
                    }
                },
                new
                {
                    category = "Database Metrics",
                    metrics = new[]
                    {
                        new { name = "db_connections_active", type = "Gauge", description = "Active database connections", labels = (string[]?)null, buckets = (double[]?)null },
                        new { name = "db_connections_idle", type = "Gauge", description = "Idle connections in pool", labels = (string[]?)null, buckets = (double[]?)null },
                        new { name = "db_query_duration_seconds", type = "Histogram", description = "Database query execution time", labels = (string[]?)new[] { "query_type", "collection" }, buckets = (double[]?)null },
                        new { name = "db_errors_total", type = "Counter", description = "Database error count by type", labels = (string[]?)new[] { "error_type" }, buckets = (double[]?)null }
                    }
                },
                new
                {
                    category = "Application Metrics",
                    metrics = new[]
                    {
                        new { name = "collections_total", type = "Gauge", description = "Number of configured collections", labels = (string[]?)null, buckets = (double[]?)null },
                        new { name = "features_returned_total", type = "Counter", description = "Total features returned", labels = (string[]?)new[] { "collection" }, buckets = (double[]?)null },
                        new { name = "spatial_queries_total", type = "Counter", description = "Spatial queries executed", labels = (string[]?)new[] { "operation" }, buckets = (double[]?)null },
                        new { name = "cache_hits_total", type = "Counter", description = "Cache hit count", labels = (string[]?)new[] { "cache_type" }, buckets = (double[]?)null },
                        new { name = "cache_misses_total", type = "Counter", description = "Cache miss count", labels = (string[]?)new[] { "cache_type" }, buckets = (double[]?)null }
                    }
                },
                new
                {
                    category = "System Metrics",
                    metrics = new[]
                    {
                        new { name = "process_cpu_usage", type = "Gauge", description = "Process CPU usage percentage", labels = (string[]?)null, buckets = (double[]?)null },
                        new { name = "process_memory_bytes", type = "Gauge", description = "Process memory usage in bytes", labels = (string[]?)null, buckets = (double[]?)null },
                        new { name = "dotnet_gc_heap_size_bytes", type = "Gauge", description = ".NET GC heap size", labels = (string[]?)new[] { "generation" }, buckets = (double[]?)null },
                        new { name = "dotnet_gc_collection_count", type = "Counter", description = "GC collection count", labels = (string[]?)new[] { "generation" }, buckets = (double[]?)null }
                    }
                }
            },
            ogcSpecificMetrics = new[]
            {
                new { name = "ogc_conformance_checks_total", type = "Counter", description = "OGC conformance validation checks", labels = (string[]?)null, buckets = (double[]?)null },
                new { name = "ogc_crs_transformations_total", type = "Counter", description = "CRS transformation operations", labels = (string[]?)new[] { "source_crs", "target_crs" }, buckets = (double[]?)null },
                new { name = "ogc_bbox_queries_total", type = "Counter", description = "Bounding box filter queries", labels = (string[]?)new[] { "collection" }, buckets = (double[]?)null },
                new { name = "geojson_encoding_duration_seconds", type = "Histogram", description = "GeoJSON encoding time", labels = (string[]?)null, buckets = (double[]?)null }
            },
            businessMetrics = new[]
            {
                new { name = "api_users_active", type = "Gauge", description = "Number of active API users", labels = (string[]?)null, buckets = (double[]?)null },
                new { name = "data_volume_bytes_served", type = "Counter", description = "Total data volume served", labels = (string[]?)null, buckets = (double[]?)null },
                new { name = "popular_collections", type = "Counter", description = "Access count by collection", labels = (string[]?)new[] { "collection_id" }, buckets = (double[]?)null }
            }
        };

        return JsonSerializer.Serialize(new
        {
            metrics,
            implementation = new
            {
                aspNetCore = @"
// Program.cs - Add Prometheus metrics
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add metrics
builder.Services.AddSingleton<IMetrics, MetricsService>();

var app = builder.Build();

// Expose metrics endpoint
app.MapMetrics(); // /metrics

// Custom middleware for request metrics
app.UseHttpMetrics();

app.Run();

// MetricsService.cs
public class MetricsService : IMetrics
{
    private static readonly Counter RequestsTotal = Metrics
        .CreateCounter(""http_requests_total"", ""Total requests"",
            new CounterConfiguration { LabelNames = new[] { ""method"", ""endpoint"", ""status_code"" } });

    private static readonly Histogram RequestDuration = Metrics
        .CreateHistogram(""http_request_duration_seconds"", ""Request duration"",
            new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) });

    public void RecordRequest(string method, string endpoint, int statusCode, double duration)
    {
        RequestsTotal.WithLabels(method, endpoint, statusCode.ToString()).Inc();
        RequestDuration.Observe(duration);
    }
}",
                pushgateway = "Use Prometheus Pushgateway for batch jobs: var pusher = new MetricPusher(endpoint: \"http://pushgateway:9091\", job: \"honua_batch\");",
                customExporter = "Implement IMetricServer interface for custom metric endpoints"
            },
            queryExamples = new
            {
                requestRate = "rate(http_requests_total[5m])",
                errorRate = "rate(http_requests_total{status_code=~\"5..\"}[5m]) / rate(http_requests_total[5m])",
                p95Latency = "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))",
                cacheHitRate = "rate(cache_hits_total[5m]) / (rate(cache_hits_total[5m]) + rate(cache_misses_total[5m]))"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Generates Prometheus scrape configuration")]
    public string GeneratePrometheusConfig(
        [Description("Services to monitor as JSON array")] string services = "[{\"name\":\"honua-server\",\"host\":\"localhost\",\"port\":5000}]")
    {
        var prometheusConfig = @"# Prometheus configuration for Honua
global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    cluster: 'honua-prod'
    environment: 'production'

# Alertmanager configuration
alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']

# Rules
rule_files:
  - 'alerts/*.yml'
  - 'recording_rules/*.yml'

# Scrape configs
scrape_configs:
  # Honua API Servers
  - job_name: 'honua-api'
    static_configs:
      - targets: ['honua-api-1:5000', 'honua-api-2:5000', 'honua-api-3:5000']
        labels:
          service: 'api'
          tier: 'frontend'

  # PostgreSQL Exporter
  - job_name: 'postgres'
    static_configs:
      - targets: ['postgres-exporter:9187']
        labels:
          service: 'database'
          tier: 'backend'

  # Node Exporter (system metrics)
  - job_name: 'node'
    static_configs:
      - targets: ['node-exporter:9100']

  # Kubernetes pods (if using K8s)
  - job_name: 'kubernetes-pods'
    kubernetes_sd_configs:
      - role: pod
    relabel_configs:
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
        action: keep
        regex: true
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
        action: replace
        target_label: __metrics_path__
        regex: (.+)
      - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
        action: replace
        regex: ([^:]+)(?::\d+)?;(\d+)
        replacement: $1:$2
        target_label: __address__

  # Redis (if using cache)
  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']
        labels:
          service: 'cache'

# Remote write (optional - for long-term storage)
remote_write:
  - url: 'https://prometheus-remote-write.example.com/api/v1/write'
    basic_auth:
      username: 'honua'
      password: 'xxx'
";

        var alertRules = @"# Alert rules for Honua
groups:
  - name: honua_alerts
    interval: 30s
    rules:
      # High error rate
      - alert: HighErrorRate
        expr: |
          rate(http_requests_total{status_code=~""5..""}[5m])
          / rate(http_requests_total[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
          service: honua-api
        annotations:
          summary: ""High error rate detected""
          description: ""Error rate is {{ $value | humanizePercentage }} (threshold: 5%)""

      # High response time
      - alert: HighResponseTime
        expr: |
          histogram_quantile(0.95,
            rate(http_request_duration_seconds_bucket[5m])
          ) > 1.0
        for: 10m
        labels:
          severity: warning
          service: honua-api
        annotations:
          summary: ""High response time""
          description: ""P95 latency is {{ $value }}s (threshold: 1s)""

      # Database connection pool exhaustion
      - alert: DatabasePoolExhausted
        expr: db_connections_active / (db_connections_active + db_connections_idle) > 0.9
        for: 5m
        labels:
          severity: critical
          service: database
        annotations:
          summary: ""Database connection pool near exhaustion""
          description: ""Pool utilization: {{ $value | humanizePercentage }}""

      # High memory usage
      - alert: HighMemoryUsage
        expr: process_memory_bytes / 1024 / 1024 / 1024 > 2
        for: 15m
        labels:
          severity: warning
          service: honua-api
        annotations:
          summary: ""High memory usage""
          description: ""Memory usage: {{ $value }}GB (threshold: 2GB)""

      # Service down
      - alert: ServiceDown
        expr: up{job=""honua-api""} == 0
        for: 1m
        labels:
          severity: critical
          service: honua-api
        annotations:
          summary: ""Honua API is down""
          description: ""Instance {{ $labels.instance }} has been down for 1 minute""

      # Low cache hit rate
      - alert: LowCacheHitRate
        expr: |
          rate(cache_hits_total[5m])
          / (rate(cache_hits_total[5m]) + rate(cache_misses_total[5m])) < 0.8
        for: 30m
        labels:
          severity: warning
          service: honua-api
        annotations:
          summary: ""Low cache hit rate""
          description: ""Cache hit rate: {{ $value | humanizePercentage }} (threshold: 80%)""
";

        return JsonSerializer.Serialize(new
        {
            prometheusConfig,
            alertRules,
            grafanaDashboard = new
            {
                dashboardJson = "Import pre-built dashboard from grafana.com/dashboards",
                panels = new[]
                {
                    "Request rate and latency",
                    "Error rate by endpoint",
                    "Database query performance",
                    "Cache hit/miss ratio",
                    "System resources (CPU, memory)",
                    "OGC API specific metrics"
                },
                importId = 12345
            },
            exporters = new[]
            {
                new { name = "postgres_exporter", image = "prometheuscommunity/postgres-exporter", port = 9187 },
                new { name = "redis_exporter", image = "oliver006/redis_exporter", port = 9121 },
                new { name = "node_exporter", image = "prom/node-exporter", port = 9100 }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Recommends alert thresholds based on SLA requirements")]
    public string RecommendAlerts(
        [Description("SLA requirements as JSON (uptime, latency, error rate)")] string slaRequirements = "{\"uptime\":99.9,\"maxLatency\":200,\"errorRate\":0.1}")
    {
        var alertRecommendations = new object[]
        {
            new
            {
                alert = "Service Availability",
                slaMetric = "99.9% uptime (43.2 min/month downtime)",
                threshold = "up{job=\"honua-api\"} == 0",
                duration = "for: 1m",
                severity = "critical",
                action = "Page on-call engineer immediately",
                mitigation = new[]
                {
                    "Check if process crashed: systemctl status honua",
                    "Review recent logs: journalctl -u honua -n 100",
                    "Restart service: systemctl restart honua",
                    "Failover to standby instance if available"
                }
            },
            new
            {
                alert = "Response Time SLA Breach",
                slaMetric = "95% of requests under 500ms",
                threshold = "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 0.5",
                duration = "for: 10m",
                severity = "warning",
                action = "Investigate performance degradation",
                mitigation = new[]
                {
                    "Check database slow queries: SELECT * FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10",
                    "Verify spatial indexes exist: SELECT * FROM pg_indexes WHERE tablename = 'layer'",
                    "Check connection pool: SELECT count(*) FROM pg_stat_activity",
                    "Scale horizontally: Add more API instances"
                }
            },
            new
            {
                alert = "Error Rate SLA Breach",
                slaMetric = "Error rate < 0.1% (1 in 1000 requests)",
                threshold = "rate(http_requests_total{status_code=~\"5..\"}[5m]) / rate(http_requests_total[5m]) > 0.001",
                duration = "for: 5m",
                severity = "critical",
                action = "Investigate and fix errors immediately",
                mitigation = new[]
                {
                    "Check error logs for patterns: grep ERROR /var/log/honua/*.log | tail -100",
                    "Verify database connectivity: psql -h db -U honua -c 'SELECT 1'",
                    "Check recent deployments: git log --since='2 hours ago'",
                    "Rollback if needed: kubectl rollout undo deployment/honua"
                }
            },
            new
            {
                alert = "Data Freshness",
                slaMetric = "Data updated within 1 hour",
                threshold = "(time() - last_data_update_timestamp) > 3600",
                duration = "for: 15m",
                severity = "warning",
                action = "Check ETL/ingestion pipeline",
                mitigation = new[]
                {
                    "Verify ingestion job status",
                    "Check source data availability",
                    "Review ETL logs for failures",
                    "Manually trigger ingestion if needed"
                }
            },
            new
            {
                alert = "Resource Exhaustion",
                slaMetric = "Prevent resource-related outages",
                conditions = new[]
                {
                    new { metric = "CPU", threshold = "process_cpu_usage > 80", duration = "for: 15m" },
                    new { metric = "Memory", threshold = "process_memory_bytes > 3GB", duration = "for: 10m" },
                    new { metric = "Disk", threshold = "node_filesystem_avail_bytes / node_filesystem_size_bytes < 0.1", duration = "for: 5m" },
                    new { metric = "DB Connections", threshold = "db_connections_active / db_connections_max > 0.9", duration = "for: 5m" }
                },
                severity = "critical",
                action = "Scale resources or optimize usage",
                mitigation = new[]
                {
                    "Scale vertically: Increase instance size",
                    "Scale horizontally: Add more instances",
                    "Optimize queries and caching",
                    "Increase connection pool size"
                }
            }
        };

        var onCallProcedure = new
        {
            escalationPolicy = new[]
            {
                new { level = 1, target = "Primary on-call engineer", notifyAfter = "Immediately", method = "PagerDuty/SMS" },
                new { level = 2, target = "Secondary on-call", notifyAfter = "15 minutes", method = "PagerDuty/Phone" },
                new { level = 3, target = "Engineering manager", notifyAfter = "30 minutes", method = "PagerDuty/Phone" },
                new { level = 4, target = "VP Engineering", notifyAfter = "1 hour", method = "Phone/Email" }
            },
            runbook = new
            {
                location = "https://wiki.example.com/honua/runbooks",
                sections = new[]
                {
                    "Initial Diagnosis",
                    "Common Issues and Fixes",
                    "Escalation Procedures",
                    "Rollback Procedures",
                    "Post-Incident Actions"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            alertRecommendations,
            onCallProcedure,
            monitoringSLA = new
            {
                alertLatency = "< 1 minute from threshold breach to notification",
                falsePositiveRate = "< 5% of alerts",
                meanTimeToDetect = "< 5 minutes",
                meanTimeToResolve = "< 2 hours for critical, < 24 hours for warnings"
            },
            alertChannels = new[]
            {
                new { channel = "PagerDuty", severity = new[] { "critical" }, behavior = "Immediate page with escalation" },
                new { channel = "Slack", severity = new[] { "warning", "critical" }, behavior = "Post to #alerts channel" },
                new { channel = "Email", severity = new[] { "warning" }, behavior = "Send to team distribution list" },
                new { channel = "Dashboard", severity = new[] { "info", "warning", "critical" }, behavior = "Display on monitoring dashboard" }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Analyzes performance trends from metrics data")]
    public string AnalyzePerformanceTrends(
        [Description("Metrics data as JSON")] string metricsData,
        [Description("Analysis period (e.g., '24h', '7d', '30d')")] string period = "24h")
    {
        var trendAnalysis = new
        {
            period,
            prometheusQueries = new
            {
                requestRateTrend = $"rate(http_requests_total[5m]) offset {period}",
                latencyTrend = $@"
-- Compare current P95 latency with period ago
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
/
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m] offset {period}))",

                errorRateTrend = $@"
-- Error rate change over period
(rate(http_requests_total{{status_code=~""5..""}}[5m])
/ rate(http_requests_total[5m]))
-
(rate(http_requests_total{{status_code=~""5..""}}[5m] offset {period})
/ rate(http_requests_total[5m] offset {period}))",

                cacheEfficiencyTrend = $@"
-- Cache hit rate trend
(rate(cache_hits_total[5m]) / (rate(cache_hits_total[5m]) + rate(cache_misses_total[5m])))
-
(rate(cache_hits_total[5m] offset {period}) / (rate(cache_hits_total[5m] offset {period}) + rate(cache_misses_total[5m] offset {period})))"
            },
            keyIndicators = new object[]
            {
                new
                {
                    indicator = "Request Volume Growth",
                    calculation = "(current_rate - historical_rate) / historical_rate * 100",
                    interpretation = new
                    {
                        positive = "> 20% growth may require capacity planning",
                        negative = "> 20% decline may indicate user issues or competition"
                    }
                },
                new
                {
                    indicator = "Performance Degradation",
                    calculation = "P95 latency trend over time",
                    interpretation = new
                    {
                        worsening = "Increasing latency indicates optimization needed or capacity issues",
                        improving = "Decreasing latency shows optimization success"
                    }
                },
                new
                {
                    indicator = "Error Rate Stability",
                    calculation = "Standard deviation of error rate",
                    interpretation = new
                    {
                        stable = "Low std dev indicates stable system",
                        unstable = "High std dev suggests intermittent issues"
                    }
                },
                new
                {
                    indicator = "Resource Utilization Trend",
                    calculation = "CPU/Memory usage trend",
                    interpretation = new
                    {
                        increasing = "Linear growth suggests scaling needed",
                        stable = "Constant usage indicates good efficiency"
                    }
                }
            },
            anomalyDetection = new
            {
                methods = new object[]
                {
                    new
                    {
                        method = "Moving Average",
                        query = "http_requests_total - avg_over_time(http_requests_total[1h])",
                        threshold = "Deviation > 3 standard deviations"
                    },
                    new
                    {
                        method = "Holt-Winters Forecasting",
                        implementation = "PromQL: holt_winters(http_requests_total[1h], 0.3, 0.1)",
                        use = "Predict expected values, alert on deviations"
                    },
                    new
                    {
                        method = "Percentile-based",
                        query = "http_request_duration_seconds > quantile_over_time(0.99, http_request_duration_seconds[24h])",
                        threshold = "Values exceeding 99th percentile"
                    }
                }
            },
            capacityPlanning = new
            {
                currentCapacity = "Based on current metrics",
                projections = new[]
                {
                    new { horizon = "1 week", calculation = "Linear extrapolation of request rate" },
                    new { horizon = "1 month", calculation = "Seasonal adjustment + trend" },
                    new { horizon = "3 months", calculation = "Business growth model + historical seasonality" }
                },
                scalingRecommendations = new
                {
                    immediate = "If CPU > 70% for 24h, add 2 more instances",
                    shortTerm = "If request rate growing > 10%/week, plan for 50% capacity increase",
                    longTerm = "Review architecture for bottlenecks, consider caching/CDN improvements"
                }
            }
        };

        return JsonSerializer.Serialize(trendAnalysis, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests logging strategy based on deployment type")]
    public string SuggestLoggingStrategy(
        [Description("Deployment type: local, cloud, or kubernetes")] string deploymentType = "local")
    {
        var loggingStrategy = (object)(deploymentType.ToLowerInvariant() switch
        {
            "kubernetes" => new
            {
                deploymentType = "Kubernetes",
                approach = "Centralized logging with log aggregation",
                implementation = new
                {
                    logFormat = "Structured JSON logging to stdout/stderr",
                    collector = "Fluent Bit or Fluentd as DaemonSet",
                    backend = "Elasticsearch, Loki, or CloudWatch Logs",
                    visualization = "Kibana, Grafana, or AWS CloudWatch Insights"
                },
                configuration = @"
# Fluent Bit ConfigMap
apiVersion: v1
kind: ConfigMap
metadata:
  name: fluent-bit-config
  namespace: logging
data:
  fluent-bit.conf: |
    [SERVICE]
        Flush         1
        Log_Level     info
        Daemon        off
        Parsers_File  parsers.conf

    [INPUT]
        Name              tail
        Path              /var/log/containers/honua*.log
        Parser            docker
        Tag               honua.*
        Refresh_Interval  5

    [FILTER]
        Name                kubernetes
        Match               honua.*
        Kube_URL            https://kubernetes.default.svc:443
        Merge_Log           On
        Keep_Log            Off

    [OUTPUT]
        Name  es
        Match honua.*
        Host  elasticsearch
        Port  9200
        Index honua-logs
        Type  _doc

  parsers.conf: |
    [PARSER]
        Name   docker
        Format json
        Time_Key time
        Time_Format %Y-%m-%dT%H:%M:%S.%L
",
                logLevels = new
                {
                    production = "Information",
                    staging = "Debug",
                    development = "Trace"
                },
                structuredLogging = @"
// appsettings.json
{
  ""Serilog"": {
    ""Using"": [ ""Serilog.Sinks.Console"" ],
    ""MinimumLevel"": {
      ""Default"": ""Information"",
      ""Override"": {
        ""Microsoft"": ""Warning"",
        ""System"": ""Warning""
      }
    },
    ""WriteTo"": [
      {
        ""Name"": ""Console"",
        ""Args"": {
          ""formatter"": ""Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact""
        }
      }
    ],
    ""Enrich"": [ ""FromLogContext"", ""WithMachineName"", ""WithThreadId"" ],
    ""Properties"": {
      ""Application"": ""Honua""
    }
  }
}"
            },
            "cloud" => new
            {
                deploymentType = "Cloud (Azure/AWS/GCP)",
                approach = "Native cloud logging services",
                implementation = new
                {
                    azure = "Application Insights + Azure Monitor Logs",
                    aws = "CloudWatch Logs + CloudWatch Insights",
                    gcp = "Cloud Logging (Stackdriver)"
                },
                configuration = @"
// Azure Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration[""ApplicationInsights:ConnectionString""];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

// AWS CloudWatch
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());
});

// GCP Cloud Logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddGoogle(new LoggerOptions
    {
        ProjectId = ""your-project-id"",
        ServiceName = ""honua-api"",
        Version = ""1.0.0""
    });
});",
                retention = "30 days for operational logs, 90 days for audit logs, 1 year for security logs",
                costOptimization = new[]
                {
                    "Use sampling for high-volume trace logs (1% sample rate)",
                    "Set appropriate retention policies to minimize storage costs",
                    "Filter out noisy health check logs",
                    "Use log levels appropriately (Info for prod, Debug for staging)"
                }
            },
            _ => new
            {
                deploymentType = "Local/Self-Hosted",
                approach = "File-based logging with rotation",
                implementation = new
                {
                    framework = "Serilog with file sinks",
                    rotation = "Daily or size-based rotation",
                    archival = "Compress and archive old logs"
                },
                configuration = @"
{
  ""Serilog"": {
    ""WriteTo"": [
      {
        ""Name"": ""Console""
      },
      {
        ""Name"": ""File"",
        ""Args"": {
          ""path"": ""/var/log/honua/honua-.log"",
          ""rollingInterval"": ""Day"",
          ""retainedFileCountLimit"": 30,
          ""rollOnFileSizeLimit"": true,
          ""fileSizeLimitBytes"": 104857600
        }
      }
    ]
  }
}",
                monitoring = "Use cron job to alert if log files grow too large or disk space low"
            }
        });

        var bestPractices = new
        {
            structuredLogging = new[]
            {
                "Always log in structured format (JSON) for easy parsing",
                "Include correlation IDs to trace requests across services",
                "Add contextual properties: userId, sessionId, requestId",
                "Use semantic log levels consistently (Trace/Debug/Info/Warn/Error/Fatal)"
            },
            sensitiveData = new[]
            {
                "Never log passwords, API keys, or tokens",
                "Redact PII (personally identifiable information)",
                "Mask credit card numbers and sensitive fields",
                "Use [Sanitized] or [Redacted] placeholders"
            },
            performance = new[]
            {
                "Use asynchronous logging to avoid blocking",
                "Implement log sampling for high-throughput scenarios",
                "Batch log writes when possible",
                "Monitor logging overhead (should be < 5% of CPU)"
            },
            whatToLog = new[]
            {
                "Request start/end with duration",
                "Database queries exceeding threshold (e.g., > 1s)",
                "Authentication successes and failures",
                "Authorization denials",
                "External API calls",
                "Data validation errors",
                "Configuration changes",
                "Health check failures"
            }
        };

        return JsonSerializer.Serialize(new
        {
            loggingStrategy,
            bestPractices,
            logAggregationTools = new[]
            {
                new { tool = "ELK Stack", components = "Elasticsearch + Logstash + Kibana", bestFor = "Self-hosted, flexible" },
                new { tool = "Grafana Loki", components = "Loki + Promtail + Grafana", bestFor = "Kubernetes, cost-effective" },
                new { tool = "Datadog", components = "Unified APM + Logs", bestFor = "SaaS, all-in-one observability" },
                new { tool = "New Relic", components = "APM + Logs + Metrics", bestFor = "SaaS, developer-friendly" },
                new { tool = "Azure Monitor", components = "Log Analytics + Application Insights", bestFor = "Azure deployments" }
            }
        }, CliJsonOptions.Indented);
    }
}
