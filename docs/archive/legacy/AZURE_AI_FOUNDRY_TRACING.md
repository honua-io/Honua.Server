# Azure AI Foundry Tracing Integration

**Date**: 2025-10-16
**Purpose**: Add comprehensive observability for Honua's multi-agent system using Azure AI Foundry and OpenTelemetry

---

## Overview

Azure AI Foundry provides unified observability for agentic systems built with Semantic Kernel. This includes:

- 🔍 **Distributed Tracing**: Track requests across agents and LLM calls
- 📊 **Metrics**: Token usage, latency, success rates
- 📝 **Logs**: Structured logging with context
- 🎯 **Agent Orchestration Visualization**: Multi-agent workflow visualization
- 📈 **Evaluations**: LLM output quality metrics

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Honua Multi-Agent System (Semantic Kernel)             │
│  ├─ ChatCompletionAgent                                 │
│  ├─ AgentGroupChat                                      │
│  └─ KernelProcess (stateful workflows)                  │
└─────────────┬───────────────────────────────────────────┘
              │ OpenTelemetry SDK
              │ (Traces, Metrics, Logs)
              ▼
┌─────────────────────────────────────────────────────────┐
│  Azure Monitor (Application Insights)                   │
│  - Receives all telemetry                               │
│  - Correlates distributed traces                        │
│  - Stores metrics & logs                                │
└─────────────┬───────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────┐
│  Azure AI Foundry Tracing UI                            │
│  - GenAI-specific visualizations                        │
│  - Agent orchestration graphs                           │
│  - Token usage analytics                                │
│  - LLM evaluation results                               │
└─────────────────────────────────────────────────────────┘
```

---

## Setup Instructions

### Step 1: Add Required NuGet Packages

```bash
cd src/Honua.Cli.AI
dotnet add package Azure.Monitor.OpenTelemetry.Exporter --version 1.3.0
dotnet add package OpenTelemetry --version 1.10.0
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.10.0
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.10.0
```

### Step 2: Create Azure Resources

#### 2.1 Create Azure AI Foundry Project

```bash
# Using Azure CLI
az extension add --name ml
az ml workspace create \
  --name honua-ai-foundry \
  --resource-group honua-rg \
  --location eastus
```

#### 2.2 Create Application Insights

```bash
# Create Application Insights resource
az monitor app-insights component create \
  --app honua-ai-insights \
  --location eastus \
  --resource-group honua-rg \
  --application-type web \
  --retention-time 90

# Get connection string
az monitor app-insights component show \
  --app honua-ai-insights \
  --resource-group honua-rg \
  --query connectionString \
  --output tsv
```

**Result**:
```
InstrumentationKey=GUID;IngestionEndpoint=https://eastus-X.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=GUID
```

#### 2.3 Link Application Insights to AI Foundry

```bash
# In Azure Portal:
# 1. Navigate to Azure AI Foundry project
# 2. Settings → Application Insights
# 3. Select the honua-ai-insights resource
```

---

## Implementation

### File 1: OpenTelemetry Configuration Service

**File**: `src/Honua.Cli.AI/Services/Observability/OpenTelemetryConfiguration.cs`

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Honua.Cli.AI.Services.Observability;

/// <summary>
/// Configures OpenTelemetry for Azure AI Foundry tracing.
/// </summary>
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddAzureAIFoundryTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["ApplicationInsights:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Telemetry disabled if no connection string
            return services;
        }

        // Enable sensitive data diagnostics for GenAI operations (token counts, prompts)
        // ⚠️ Only enable in dev/staging, NOT in production with customer data
        var enableSensitiveData = configuration.GetValue<bool>(
            "ApplicationInsights:EnableSensitiveDiagnostics",
            false);

        if (enableSensitiveData)
        {
            AppContext.SetSwitch(
                "Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive",
                true);
        }

        // Create resource builder with service metadata
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: "Honua.AI.MultiAgent",
                serviceVersion: typeof(OpenTelemetryConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = configuration["Environment"] ?? "development",
                ["service.namespace"] = "Honua.GIS.AI"
            });

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("Honua.AI.MultiAgent"))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    // Semantic Kernel traces
                    .AddSource("Microsoft.SemanticKernel*")
                    // Agent Framework traces
                    .AddSource("Microsoft.SemanticKernel.Agents*")
                    // Process Framework traces
                    .AddSource("Microsoft.SemanticKernel.Process*")
                    // Honua custom traces
                    .AddSource("Honua.AI*")
                    // Azure SDK traces (for Azure OpenAI calls)
                    .AddSource("Azure.AI.OpenAI*")
                    // HTTP client traces
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            // Add custom attributes
                            if (request.RequestUri?.Host.Contains("openai") == true)
                            {
                                activity.SetTag("llm.provider", "azure_openai");
                            }
                            else if (request.RequestUri?.Host.Contains("anthropic") == true)
                            {
                                activity.SetTag("llm.provider", "anthropic");
                            }
                        };
                    })
                    // Export to Azure Monitor
                    .AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    // Semantic Kernel metrics (token usage, latency)
                    .AddMeter("Microsoft.SemanticKernel*")
                    // Agent metrics
                    .AddMeter("Microsoft.SemanticKernel.Agents*")
                    // Process metrics
                    .AddMeter("Microsoft.SemanticKernel.Process*")
                    // Honua custom metrics
                    .AddMeter("Honua.AI*")
                    // Runtime metrics
                    .AddRuntimeInstrumentation()
                    // HTTP metrics
                    .AddHttpClientInstrumentation()
                    // Export to Azure Monitor
                    .AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
            });

        // Add OpenTelemetry logging
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddAzureMonitorLogExporter(exporter =>
                {
                    exporter.ConnectionString = connectionString;
                });
            });
        });

        return services;
    }
}
```

### File 2: Custom Activity Source for Agent Operations

**File**: `src/Honua.Cli.AI/Services/Observability/AgentActivitySource.cs`

```csharp
using System.Diagnostics;

namespace Honua.Cli.AI.Services.Observability;

/// <summary>
/// Custom ActivitySource for tracing Honua agent operations.
/// </summary>
public static class AgentActivitySource
{
    private static readonly ActivitySource Source = new("Honua.AI.Agents", "1.0.0");

    /// <summary>
    /// Start tracing an agent invocation.
    /// </summary>
    public static Activity? StartAgentInvocation(
        string agentName,
        string intent,
        Dictionary<string, object>? tags = null)
    {
        var activity = Source.StartActivity(
            $"Agent.{agentName}.Invoke",
            ActivityKind.Internal);

        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("agent.intent", intent);
        activity?.SetTag("agent.framework", "semantic_kernel");

        if (tags != null)
        {
            foreach (var (key, value) in tags)
            {
                activity?.SetTag(key, value);
            }
        }

        return activity;
    }

    /// <summary>
    /// Start tracing multi-agent orchestration.
    /// </summary>
    public static Activity? StartGroupChat(
        string[] agentNames,
        string orchestrationStrategy)
    {
        var activity = Source.StartActivity(
            "AgentGroupChat.Orchestrate",
            ActivityKind.Internal);

        activity?.SetTag("agent.group.size", agentNames.Length);
        activity?.SetTag("agent.group.members", string.Join(",", agentNames));
        activity?.SetTag("agent.orchestration.strategy", orchestrationStrategy);

        return activity;
    }

    /// <summary>
    /// Start tracing a process workflow.
    /// </summary>
    public static Activity? StartProcess(
        string processName,
        int stepCount)
    {
        var activity = Source.StartActivity(
            $"Process.{processName}.Execute",
            ActivityKind.Internal);

        activity?.SetTag("process.name", processName);
        activity?.SetTag("process.step_count", stepCount);
        activity?.SetTag("process.framework", "semantic_kernel");

        return activity;
    }

    /// <summary>
    /// Record an agent decision or action.
    /// </summary>
    public static void RecordAgentDecision(
        this Activity? activity,
        string decision,
        double? confidenceScore = null)
    {
        activity?.AddEvent(new ActivityEvent("agent.decision", tags: new ActivityTagsCollection
        {
            { "decision", decision },
            { "confidence", confidenceScore ?? 0.0 }
        }));
    }

    /// <summary>
    /// Record guard validation result.
    /// </summary>
    public static void RecordGuardValidation(
        this Activity? activity,
        string guardType,
        bool isSafe,
        double confidenceScore,
        string[]? threats = null)
    {
        activity?.AddEvent(new ActivityEvent("guard.validation", tags: new ActivityTagsCollection
        {
            { "guard.type", guardType },
            { "guard.is_safe", isSafe },
            { "guard.confidence", confidenceScore },
            { "guard.threats", threats != null ? string.Join(",", threats) : "none" }
        }));
    }

    /// <summary>
    /// Set agent result status.
    /// </summary>
    public static void SetAgentResult(
        this Activity? activity,
        bool success,
        string? errorMessage = null,
        int? tokenCount = null)
    {
        activity?.SetTag("agent.success", success);

        if (errorMessage != null)
        {
            activity?.SetTag("agent.error", errorMessage);
            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        if (tokenCount.HasValue)
        {
            activity?.SetTag("agent.token_count", tokenCount.Value);
        }
    }
}
```

### File 3: Update DI Registration

**File**: `src/Honua.Cli.AI/Extensions/AzureAIServiceCollectionExtensions.cs`

Add to the `AddAzureAI` method:

```csharp
public static IServiceCollection AddAzureAI(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... existing code ...

    // Add Azure AI Foundry Telemetry (BEFORE keyed services)
    services.AddAzureAIFoundryTelemetry(configuration);

    // ... rest of existing code ...

    return services;
}
```

### File 4: Configuration

**File**: `src/Honua.Cli.AI/appsettings.Azure.json`

Add configuration section:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "",
    "EnableSensitiveDiagnostics": false,
    "SamplingPercentage": 100.0,
    "EnableAdaptiveSampling": true
  },
  "OpenTelemetry": {
    "ServiceName": "Honua.AI.MultiAgent",
    "ServiceVersion": "1.0.0",
    "ExportIntervalMilliseconds": 5000
  }
}
```

**File**: `tests/Honua.Cli.AI.Tests/appsettings.test.json`

```json
{
  "ApplicationInsights": {
    "ConnectionString": "",
    "EnableSensitiveDiagnostics": true,
    "SamplingPercentage": 100.0
  }
}
```

---

## Usage in Agents

### Example 1: Tracing Single Agent Invocation

```csharp
public async Task<string> GenerateConfigurationAsync(
    string request,
    AgentExecutionContext context,
    CancellationToken cancellationToken = default)
{
    // Start custom trace span
    using var activity = AgentActivitySource.StartAgentInvocation(
        "DeploymentConfiguration",
        "generate_infrastructure",
        new Dictionary<string, object>
        {
            ["provider"] = "aws",
            ["dry_run"] = context.DryRun
        });

    try
    {
        // SK Agent Framework handles internal tracing automatically
        var thread = new AgentThread();
        string? response = null;

        await foreach (var message in _agent.InvokeAsync(thread, request, cancellationToken))
        {
            if (message.Role == AuthorRole.Assistant)
            {
                response = message.Content;
            }
        }

        // Record success
        activity?.SetAgentResult(success: true, tokenCount: 1500);

        return response ?? "No response generated";
    }
    catch (Exception ex)
    {
        // Record failure
        activity?.SetAgentResult(success: false, errorMessage: ex.Message);
        throw;
    }
}
```

### Example 2: Tracing AgentGroupChat

```csharp
public async Task<string> ConsultAsync(
    string userRequest,
    CancellationToken cancellationToken = default)
{
    // Start group chat trace
    using var activity = AgentActivitySource.StartGroupChat(
        new[] { "ArchitectureConsultant", "CostReviewer" },
        "kernel_function_selection");

    var responses = new List<string>();

    try
    {
        // SK automatically traces each agent interaction
        await foreach (var message in _chat.InvokeAsync(userRequest, cancellationToken))
        {
            responses.Add($"[{message.AuthorName}]: {message.Content}");

            // Record agent turns
            activity?.AddEvent(new ActivityEvent($"agent.turn.{message.AuthorName}"));
        }

        activity?.SetAgentResult(success: true);
        return string.Join("\n\n", responses);
    }
    catch (Exception ex)
    {
        activity?.SetAgentResult(success: false, errorMessage: ex.Message);
        throw;
    }
}
```

### Example 3: Tracing with Guards

```csharp
// Input Guard with tracing
var inputCheck = await _inputGuard.ValidateInputAsync(
    request,
    "Honua GIS deployment consultant",
    cancellationToken);

// Record guard validation in trace
activity?.RecordGuardValidation(
    "input_guard",
    inputCheck.IsSafe,
    inputCheck.ConfidenceScore,
    inputCheck.DetectedThreats);

if (!inputCheck.IsSafe)
{
    activity?.SetAgentResult(success: false, errorMessage: "Input blocked by guard");
    return BlockedResult(inputCheck);
}
```

---

## What Gets Traced Automatically

### Semantic Kernel Native Telemetry

SK automatically emits telemetry for:

1. **LLM Calls**:
   - Model name, provider
   - Token counts (prompt, completion, total)
   - Latency
   - Temperature, max_tokens settings

2. **Function Calling**:
   - Function name
   - Parameters
   - Execution time
   - Success/failure

3. **Prompts**:
   - System prompts (if sensitive diagnostics enabled)
   - User messages (if sensitive diagnostics enabled)
   - Token usage

4. **Agents**:
   - Agent name
   - Conversation history length
   - Agent orchestration decisions

5. **Processes**:
   - Step names
   - State transitions
   - Event emissions

---

## Viewing Traces in Azure AI Foundry

### Step 1: Navigate to Tracing UI

1. Open Azure Portal
2. Navigate to your Azure AI Foundry project
3. Select **Tracing** from left menu

### Step 2: View Agent Orchestration

The UI shows:

- **Waterfall view**: Timeline of agent interactions
- **Agent graph**: Visual representation of multi-agent conversations
- **Token usage**: Per-agent and total token consumption
- **Latency**: Response times for each agent
- **Errors**: Failed operations with stack traces

### Step 3: Filter and Search

- Filter by agent name
- Filter by date range
- Search by trace ID or user request
- Filter by success/failure

---

## Custom Metrics

### Example: Agent Performance Metrics

```csharp
using System.Diagnostics.Metrics;

public class AgentMetrics
{
    private static readonly Meter Meter = new("Honua.AI.Agents", "1.0.0");

    private static readonly Counter<long> AgentInvocations = Meter.CreateCounter<long>(
        "agent.invocations",
        unit: "invocations",
        description: "Number of agent invocations");

    private static readonly Histogram<double> AgentLatency = Meter.CreateHistogram<double>(
        "agent.latency",
        unit: "ms",
        description: "Agent response latency");

    private static readonly Counter<long> AgentTokens = Meter.CreateCounter<long>(
        "agent.tokens",
        unit: "tokens",
        description: "Total tokens used by agents");

    public static void RecordInvocation(string agentName, bool success)
    {
        AgentInvocations.Add(1, new KeyValuePair<string, object?>("agent.name", agentName),
                                  new KeyValuePair<string, object?>("success", success));
    }

    public static void RecordLatency(string agentName, double latencyMs)
    {
        AgentLatency.Record(latencyMs, new KeyValuePair<string, object?>("agent.name", agentName));
    }

    public static void RecordTokens(string agentName, long tokenCount)
    {
        AgentTokens.Add(tokenCount, new KeyValuePair<string, object?>("agent.name", agentName));
    }
}
```

---

## Best Practices

### 1. **Enable Sensitive Diagnostics Only in Non-Production**

```json
{
  "ApplicationInsights": {
    "EnableSensitiveDiagnostics": false  // ALWAYS false in production
  }
}
```

Sensitive diagnostics include prompts and completions, which may contain customer data.

### 2. **Use Sampling for High-Volume Scenarios**

```json
{
  "ApplicationInsights": {
    "SamplingPercentage": 10.0,  // Sample 10% of traces
    "EnableAdaptiveSampling": true
  }
}
```

### 3. **Add Custom Tags for Business Context**

```csharp
activity?.SetTag("deployment.provider", "aws");
activity?.SetTag("deployment.region", "us-west-2");
activity?.SetTag("user.organization", "acme-corp");
```

### 4. **Correlate Traces with Request IDs**

```csharp
activity?.SetTag("request.id", context.RequestId);
activity?.SetTag("session.id", context.SessionId);
```

### 5. **Monitor Token Usage Costs**

```csharp
activity?.SetTag("cost.estimated_usd", estimatedCost);
activity?.SetTag("tokens.prompt", promptTokens);
activity?.SetTag("tokens.completion", completionTokens);
```

---

## Troubleshooting

### Issue: Traces Not Appearing in AI Foundry

**Known Issue**: C#/.NET traces may appear in Application Insights but not in AI Foundry Tracing UI (as of 2025-01).

**Workarounds**:
1. Verify Application Insights is linked to AI Foundry project
2. Check that traces appear in Application Insights first
3. Wait 5-10 minutes for traces to propagate
4. Ensure using `Azure.Monitor.OpenTelemetry.Exporter` 1.3.0+

**GitHub Issue**: [#13106](https://github.com/microsoft/semantic-kernel/issues/13106)

### Issue: Missing Token Counts

**Solution**: Enable sensitive diagnostics (dev/staging only):

```csharp
AppContext.SetSwitch(
    "Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive",
    true);
```

### Issue: High Telemetry Volume

**Solution**: Enable adaptive sampling:

```json
{
  "ApplicationInsights": {
    "EnableAdaptiveSampling": true,
    "SamplingPercentage": 20.0
  }
}
```

---

## Cost Considerations

### Application Insights Pricing

- **Data Ingestion**: $2.88 per GB (after 5GB free per month)
- **Data Retention**: 90 days free, $0.12 per GB per month after

### Estimated Costs for Honua

**Assumptions**:
- 1000 agent invocations/day
- Average 5 traces per invocation (agent + LLM calls)
- Average 10 KB per trace

**Calculation**:
- 1000 invocations × 5 traces × 10 KB = 50 MB/day
- 50 MB/day × 30 days = 1.5 GB/month
- Cost: ~$0 (under 5GB free tier)

**Recommendation**: Start with 100% sampling, adjust based on volume.

---

## Next Steps

1. ✅ Add NuGet packages
2. ✅ Create Azure resources (AI Foundry + Application Insights)
3. ✅ Add connection string to configuration
4. ✅ Register telemetry in DI container
5. ✅ Add custom tracing to agents
6. ✅ Test traces appear in Application Insights
7. ✅ Verify traces appear in AI Foundry UI
8. ✅ Create dashboards for agent performance
9. ✅ Set up alerts for failures

---

## References

- [SK Telemetry Documentation](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/docs/TELEMETRY.md)
- [Azure AI Foundry Tracing](https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/observability/telemetry-with-azure-ai-foundry-tracing)
- [Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-configuration)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
