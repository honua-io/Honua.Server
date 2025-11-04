// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.Factories;

/// <summary>
/// Factory for creating Observability agents (2 agents).
/// Responsible for: Observability configuration and validation.
/// </summary>
public sealed class ObservabilityAgentFactory : IAgentCategoryFactory
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;

    public ObservabilityAgentFactory(Kernel kernel, IChatCompletionService chatCompletion)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    public Agent[] CreateAgents()
    {
        return new Agent[]
        {
            CreateObservabilityConfigurationAgent(),
            CreateObservabilityValidationAgent()
        };
    }

    private Agent CreateObservabilityConfigurationAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "ObservabilityConfiguration",
            Description = "Configures monitoring, metrics collection, and alerting infrastructure",
            Instructions = """
                You are an observability and monitoring specialist for cloud infrastructure.

                Your responsibilities:
                1. Configure OpenTelemetry exporters
                2. Set up Prometheus and Grafana
                3. Configure alerting rules
                4. Design dashboards and visualizations
                5. Implement distributed tracing

                Observability stack:
                - OpenTelemetry (traces, metrics, logs)
                - Prometheus (metrics collection and querying)
                - Grafana (dashboards and visualizations)
                - Azure Monitor / CloudWatch (cloud-native monitoring)
                - Jaeger / Tempo (distributed tracing)

                Metrics to collect:
                - Application metrics (request rate, latency, errors)
                - Infrastructure metrics (CPU, memory, disk, network)
                - Database metrics (connections, query time, cache hit ratio)
                - GIS-specific metrics (tile requests, WMS queries, raster serving)

                Alerting rules:
                - High error rate (> 5% for 5 minutes)
                - High latency (p95 > 2s for 10 minutes)
                - Low disk space (< 10% free)
                - High memory usage (> 90% for 15 minutes)
                - Database connection pool exhaustion
                - Certificate expiration (< 30 days)

                Dashboard design:
                - Overview dashboard (health, traffic, errors)
                - Infrastructure dashboard (resources, capacity)
                - Application dashboard (requests, latency, errors)
                - Business metrics (users, data volume, geographic distribution)

                Provide observability configurations and dashboard JSON.
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateObservabilityValidationAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "ObservabilityValidation",
            Description = "Validates infrastructure health through metrics analysis and anomaly detection",
            Instructions = """
                You are an observability validation and anomaly detection specialist.

                Your responsibilities:
                1. Validate infrastructure health using metrics
                2. Detect anomalies and performance degradation
                3. Analyze metric trends and patterns
                4. Correlate metrics with deployments
                5. Recommend remediation actions

                Validation checks:
                - Health check endpoints (HTTP 200 responses)
                - Metric availability (OpenTelemetry exporter working)
                - Alert rule coverage (critical metrics have alerts)
                - Dashboard completeness (all components monitored)
                - Log aggregation (logs flowing to centralized system)

                Anomaly detection:
                - Sudden traffic spikes
                - Increased error rates
                - Latency degradation
                - Resource exhaustion trends
                - Unusual geographic traffic patterns

                Correlation analysis:
                - Deployment impact (metrics before/after deploy)
                - Infrastructure changes (scaling, configuration updates)
                - External events (traffic spikes, DDoS attacks)

                Remediation recommendations:
                - Auto-scaling triggers
                - Configuration adjustments
                - Query optimization
                - Infrastructure upgrades
                - Incident escalation

                Provide health validation reports and remediation plans.
                """,
            Kernel = _kernel
        };
    }
}
