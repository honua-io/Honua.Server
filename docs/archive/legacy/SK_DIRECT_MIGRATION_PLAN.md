# Semantic Kernel Direct Migration Plan

**Date**: 2025-10-16
**Strategy**: Clean migration - Replace custom implementation with SK framework directly
**No Production**: Design it right from the start

---

## 🎯 Philosophy

Since there's no production system to protect, we'll **architect it correctly from day one** using:

- ✅ **SK Agent Framework** for ALL agents (no custom coordinator)
- ✅ **SK Process Framework** for stateful workflows
- ✅ **Azure AI Foundry** for observability
- ✅ **Modern patterns** throughout (no legacy code)

---

## 🏗️ Target Architecture

### Current (Custom)
```
SemanticAgentCoordinator (custom)
├─ Manual routing (switch statements)
├─ Custom conversation history
├─ Manual agent instantiation
└─ 20+ Specialized agents (custom base)
```

### Target (SK Framework)
```
AgentGroupCoordinator (SK-based)
├─ KernelFunctionSelectionStrategy
├─ KernelFunctionTerminationStrategy
├─ Built-in ChatHistory
└─ ChatCompletionAgent[] (SK framework)
    ├─ Each agent = ChatCompletionAgent instance
    ├─ Plugins for capabilities
    └─ Native SK telemetry
```

---

## 📋 Migration Steps

### Phase 1: Core Infrastructure ✅ **READY**

**Duration**: 1 day

#### 1.1 Packages Already Added ✅
- `Microsoft.SemanticKernel.Agents.Core` 1.65.0
- `Microsoft.SemanticKernel.Process.Core` 1.66.0-alpha
- `Azure.Monitor.OpenTelemetry.Exporter` 1.3.0
- OpenTelemetry packages

#### 1.2 Create Foundational Services

**File**: `src/Honua.Cli.AI/Services/Observability/OpenTelemetryConfiguration.cs`

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Honua.Cli.AI.Services.Observability;

public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddHonuaAITelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services; // Telemetry optional for development
        }

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService("Honua.AI", "1.0.0");

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resourceBuilder)
                    .AddSource("Microsoft.SemanticKernel*")
                    .AddSource("Honua.AI*")
                    .AddHttpClientInstrumentation()
                    .AddAzureMonitorTraceExporter(options =>
                        options.ConnectionString = connectionString);
            })
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resourceBuilder)
                    .AddMeter("Microsoft.SemanticKernel*")
                    .AddMeter("Honua.AI*")
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddAzureMonitorMetricExporter(options =>
                        options.ConnectionString = connectionString);
            });

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.AddAzureMonitorLogExporter(exporter =>
                    exporter.ConnectionString = connectionString);
            });
        });

        return services;
    }
}
```

---

### Phase 2: Agent Framework Foundation 🎯 **START HERE**

**Duration**: 2 days

#### 2.1 Create Agent Registry

Instead of manual switch statements, use a registry pattern:

**File**: `src/Honua.Cli.AI/Services/Agents/SK/AgentRegistry.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.SK;

/// <summary>
/// Registry of all Honua AI agents using SK Agent Framework.
/// Replaces manual switch statement with clean registration.
/// </summary>
public interface IAgentRegistry
{
    ChatCompletionAgent GetAgent(string agentName);
    IEnumerable<string> GetAllAgentNames();
}

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly Dictionary<string, ChatCompletionAgent> _agents = new();
    private readonly Kernel _kernel;

    public AgentRegistry(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        RegisterAllAgents();
    }

    private void RegisterAllAgents()
    {
        // Architecture & Planning
        RegisterAgent("ArchitectureConsulting", CreateArchitectureAgent());
        RegisterAgent("CostReview", CreateCostReviewAgent());
        RegisterAgent("SecurityReview", CreateSecurityReviewAgent());

        // Configuration & Deployment
        RegisterAgent("DeploymentConfiguration", CreateDeploymentConfigAgent());
        RegisterAgent("DeploymentExecution", CreateDeploymentExecutionAgent());
        RegisterAgent("DeploymentTopology", CreateTopologyAgent());

        // Operations
        RegisterAgent("BlueGreenDeployment", CreateBlueGreenAgent());
        RegisterAgent("CertificateManagement", CreateCertificateAgent());
        RegisterAgent("DnsConfiguration", CreateDnsAgent());
        RegisterAgent("GitOpsConfiguration", CreateGitOpsAgent());

        // Performance & Optimization
        RegisterAgent("PerformanceBenchmark", CreateBenchmarkAgent());
        RegisterAgent("PerformanceOptimization", CreateOptimizationAgent());
        RegisterAgent("DatabaseOptimization", CreateDatabaseOptAgent());

        // Security & Compliance
        RegisterAgent("SecurityHardening", CreateSecurityAgent());
        RegisterAgent("ComplianceAgent", CreateComplianceAgent());

        // Data & Migration
        RegisterAgent("DataIngestion", CreateDataIngestionAgent());
        RegisterAgent("MigrationImport", CreateMigrationAgent());

        // Troubleshooting & Diagnostics
        RegisterAgent("Troubleshooting", CreateTroubleshootingAgent());
        RegisterAgent("NetworkDiagnostics", CreateNetworkDiagAgent());

        // Observability
        RegisterAgent("ObservabilityConfiguration", CreateObservabilityAgent());
        RegisterAgent("ObservabilityValidation", CreateObsValidationAgent());

        // Advanced
        RegisterAgent("HonuaUpgrade", CreateUpgradeAgent());
        RegisterAgent("DisasterRecovery", CreateDisasterRecoveryAgent());
        RegisterAgent("SpaDeployment", CreateSpaAgent());
    }

    private void RegisterAgent(string name, ChatCompletionAgent agent)
    {
        _agents[name] = agent;
    }

    public ChatCompletionAgent GetAgent(string agentName)
    {
        if (_agents.TryGetValue(agentName, out var agent))
        {
            return agent;
        }
        throw new InvalidOperationException($"Agent '{agentName}' not found");
    }

    public IEnumerable<string> GetAllAgentNames() => _agents.Keys;

    // Agent Factory Methods

    private ChatCompletionAgent CreateArchitectureAgent() => new()
    {
        Name = "ArchitectureConsulting",
        Instructions = """
            You are an Architecture Consulting specialist for Honua GIS deployments.

            Your responsibilities:
            - Analyze requirements (users, data volume, traffic, budget)
            - Present multiple architecture options (serverless, K8s, VMs, hybrid)
            - Compare cost vs. performance trade-offs
            - Recommend cloud provider and deployment strategy
            - Explain complexity, scalability, operational burden

            Focus on understanding needs and guiding to the right design decision.
            Always provide 2-3 options with pros/cons.
            """,
        Kernel = _kernel,
        Arguments = new KernelArguments(
            new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.7,
                MaxTokens = 4000
            })
    };

    private ChatCompletionAgent CreateDeploymentConfigAgent() => new()
    {
        Name = "DeploymentConfiguration",
        Instructions = """
            You are the Deployment Configuration specialist.

            Your responsibilities:
            - Generate infrastructure code (Terraform, docker-compose, Kubernetes manifests)
            - Cloud deployment configuration (AWS, Azure, GCP)
            - OGC metadata generation and updates
            - Service configuration (WFS, WMS, OGC API Features)
            - GitOps workflows for environment promotion

            Focus on declarative, version-controlled configuration changes.
            Always include comments explaining each section.
            Validate syntax before returning.
            """,
        Kernel = _kernel,
        Arguments = new KernelArguments(
            new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.3,
                MaxTokens = 8000
            })
    };

    private ChatCompletionAgent CreateCostReviewAgent() => new()
    {
        Name = "CostReview",
        Instructions = """
            You are a Cost Review specialist.

            Your responsibilities:
            - Analyze infrastructure costs
            - Identify expensive services and suggest alternatives
            - Calculate Total Cost of Ownership (TCO)
            - Provide cost breakdowns by service
            - Suggest cost optimization strategies

            Always provide specific $ estimates when possible.
            Compare alternatives with cost differences.
            """,
        Kernel = _kernel,
        Arguments = new KernelArguments(
            new OpenAIPromptExecutionSettings
            {
                Temperature = 0.2,
                MaxTokens = 3000
            })
    };

    // TODO: Implement remaining 20+ agent factories
    // Each follows same pattern: Name, Instructions, Kernel, Arguments
}
```

#### 2.2 Create SK-Based Coordinator

**File**: `src/Honua.Cli.AI/Services/Agents/SK/SKAgentCoordinator.cs`

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Honua.Cli.AI.Services.Guards;

namespace Honua.Cli.AI.Services.Agents.SK;

/// <summary>
/// SK Agent Framework-based coordinator.
/// Replaces custom SemanticAgentCoordinator with official SK patterns.
/// </summary>
public sealed class SKAgentCoordinator : IAgentCoordinator
{
    private readonly IAgentRegistry _registry;
    private readonly Kernel _kernel;
    private readonly IInputGuard? _inputGuard;
    private readonly IOutputGuard? _outputGuard;
    private readonly ILogger<SKAgentCoordinator> _logger;
    private static readonly ActivitySource ActivitySource = new("Honua.AI.Coordinator");

    public SKAgentCoordinator(
        IAgentRegistry registry,
        Kernel kernel,
        ILogger<SKAgentCoordinator> logger,
        IInputGuard? inputGuard = null,
        IOutputGuard? outputGuard = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inputGuard = inputGuard;
        _outputGuard = outputGuard;
    }

    public async Task<AgentCoordinatorResult> ProcessRequestAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AgentCoordinator.Process");
        activity?.SetTag("request", request);
        activity?.SetTag("dry_run", context.DryRun);

        var startTime = DateTime.UtcNow;
        var warnings = new List<string>();

        try
        {
            // Phase 1: Input Guard
            if (_inputGuard != null)
            {
                var inputCheck = await _inputGuard.ValidateInputAsync(
                    request,
                    "Honua GIS deployment consultant",
                    cancellationToken);

                activity?.AddEvent(new ActivityEvent("guard.input", tags: new ActivityTagsCollection
                {
                    { "is_safe", inputCheck.IsSafe },
                    { "confidence", inputCheck.ConfidenceScore }
                }));

                if (!inputCheck.IsSafe)
                {
                    _logger.LogWarning("Input blocked: {Threats}",
                        string.Join(", ", inputCheck.DetectedThreats));

                    return new AgentCoordinatorResult
                    {
                        Success = false,
                        Response = $"Request blocked for safety: {inputCheck.Explanation}",
                        AgentsInvolved = new List<string> { "InputGuard" },
                        Warnings = new List<string> { $"Threats: {string.Join(", ", inputCheck.DetectedThreats)}" }
                    };
                }
            }

            // Phase 2: Intent Analysis (using LLM)
            var intent = await AnalyzeIntentAsync(request, cancellationToken);
            activity?.SetTag("intent", intent.PrimaryIntent);
            activity?.SetTag("agents_required", string.Join(",", intent.RequiredAgents));

            // Phase 3: Route to Agent(s)
            string response;
            List<string> agentsInvolved;

            if (intent.RequiresMultipleAgents)
            {
                // Multi-agent using AgentGroupChat
                (response, agentsInvolved) = await ExecuteGroupChatAsync(
                    intent,
                    request,
                    cancellationToken);
            }
            else
            {
                // Single agent
                var agentName = intent.RequiredAgents.FirstOrDefault() ?? "DeploymentConfiguration";
                (response, agentsInvolved) = await ExecuteSingleAgentAsync(
                    agentName,
                    request,
                    cancellationToken);
            }

            // Phase 4: Output Guard
            if (_outputGuard != null)
            {
                var outputCheck = await _outputGuard.ValidateOutputAsync(
                    response,
                    string.Join(",", agentsInvolved),
                    request,
                    cancellationToken);

                activity?.AddEvent(new ActivityEvent("guard.output", tags: new ActivityTagsCollection
                {
                    { "is_safe", outputCheck.IsSafe },
                    { "hallucination_risk", outputCheck.HallucinationRisk }
                }));

                if (!outputCheck.IsSafe)
                {
                    return new AgentCoordinatorResult
                    {
                        Success = false,
                        Response = $"Agent output blocked: {outputCheck.Explanation}",
                        AgentsInvolved = agentsInvolved,
                        Warnings = outputCheck.DetectedIssues.ToList()
                    };
                }

                if (outputCheck.HallucinationRisk > 0.6)
                {
                    warnings.Add($"⚠️ High hallucination risk ({outputCheck.HallucinationRisk:P0})");
                }

                if (outputCheck.ContainsDangerousOperations)
                {
                    warnings.Add("⚠️ Contains potentially dangerous operations");
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok);

            return new AgentCoordinatorResult
            {
                Success = true,
                Response = response,
                AgentsInvolved = agentsInvolved,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            _logger.LogError(ex, "Agent coordination failed");

            return new AgentCoordinatorResult
            {
                Success = false,
                ErrorMessage = $"Agent coordination failed: {ex.Message}",
                AgentsInvolved = new List<string>()
            };
        }
    }

    private async Task<IntentAnalysisResult> AnalyzeIntentAsync(
        string request,
        CancellationToken cancellationToken)
    {
        // Use LLM to classify intent and required agents
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = """
            Analyze the user request and determine:
            1. Primary intent (deployment, architecture, troubleshooting, etc.)
            2. Which specialized agents are needed
            3. Whether multiple agents should collaborate

            Available agents:
            - ArchitectureConsulting: Design decisions, cost analysis
            - DeploymentConfiguration: Generate infrastructure code
            - DeploymentExecution: Execute deployments
            - CostReview: Cost analysis
            - SecurityReview: Security audit
            - Troubleshooting: Diagnose issues
            - PerformanceBenchmark: Load testing
            - PerformanceOptimization: Optimization
            - SecurityHardening: Security setup
            - BlueGreenDeployment: Zero-downtime deployments
            - And 10+ more...

            Respond with JSON:
            {
              "primaryIntent": "intent",
              "requiredAgents": ["agent1", "agent2"],
              "requiresMultipleAgents": true/false
            }
            """;

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(request);

        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            new OpenAIPromptExecutionSettings { Temperature = 0.1, MaxTokens = 500 },
            kernel: _kernel,
            cancellationToken: cancellationToken);

        // Parse JSON response
        return ParseIntentJson(result.Content ?? "{}");
    }

    private async Task<(string response, List<string> agents)> ExecuteSingleAgentAsync(
        string agentName,
        string request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity($"Agent.{agentName}");

        var agent = _registry.GetAgent(agentName);
        var thread = new AgentThread();

        var responses = new List<string>();

        await foreach (var message in agent.InvokeAsync(thread, request, cancellationToken))
        {
            if (message.Role == AuthorRole.Assistant)
            {
                responses.Add(message.Content ?? "");
            }
        }

        return (string.Join("\n", responses), new List<string> { agentName });
    }

    private async Task<(string response, List<string> agents)> ExecuteGroupChatAsync(
        IntentAnalysisResult intent,
        string request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("AgentGroupChat");

        // Get required agents from registry
        var agents = intent.RequiredAgents
            .Select(name => _registry.GetAgent(name))
            .ToArray();

        // Create selection strategy
        var selectionFunction = AgentGroupChat.CreatePromptFunctionForStrategy(
            """
            Given the conversation history, choose the next agent to respond.

            {{$lastmessage}}

            Return only the agent name.
            """,
            safeParameterNames: "lastmessage");

        // Create termination strategy
        var terminationFunction = AgentGroupChat.CreatePromptFunctionForStrategy(
            """
            Determine if the user's request has been fully addressed.

            {{$lastmessage}}

            Return "yes" if complete, "no" otherwise.
            """,
            safeParameterNames: "lastmessage");

        // Create group chat
        var chat = new AgentGroupChat(agents)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new KernelFunctionSelectionStrategy(selectionFunction, _kernel)
                {
                    InitialAgent = agents[0],
                    ResultParser = result => result.GetValue<string>() ?? agents[0].Name
                },
                TerminationStrategy = new KernelFunctionTerminationStrategy(terminationFunction, _kernel)
                {
                    MaximumIterations = 10,
                    ResultParser = result =>
                        result.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false
                }
            }
        };

        // Invoke group chat
        var responses = new List<string>();
        await foreach (var message in chat.InvokeAsync(request, cancellationToken))
        {
            responses.Add($"[{message.AuthorName}]: {message.Content}");
        }

        return (string.Join("\n\n", responses), agents.Select(a => a.Name).ToList());
    }

    private IntentAnalysisResult ParseIntentJson(string json)
    {
        // TODO: Implement robust JSON parsing
        return new IntentAnalysisResult
        {
            PrimaryIntent = "deployment",
            RequiredAgents = new List<string> { "DeploymentConfiguration" },
            RequiresMultipleAgents = false
        };
    }

    public Task<AgentInteractionHistory> GetHistoryAsync()
    {
        // SK agents maintain their own conversation history
        throw new NotImplementedException("Use AgentThread.GetMessagesAsync() instead");
    }
}
```

---

### Phase 3: Process Framework Integration

**Duration**: 2 days

#### 3.1 Create Process Base Infrastructure

**File**: `src/Honua.Cli.AI/Services/Processes/ProcessRegistry.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Registry of all stateful workflows using SK Process Framework.
/// </summary>
public interface IProcessRegistry
{
    KernelProcess GetProcess(string processName);
    IEnumerable<string> GetAllProcessNames();
}

public sealed class ProcessRegistry : IProcessRegistry
{
    private readonly Dictionary<string, KernelProcess> _processes = new();

    public ProcessRegistry()
    {
        RegisterAllProcesses();
    }

    private void RegisterAllProcesses()
    {
        RegisterProcess("BlueGreenDeployment", BlueGreenDeploymentProcess.Create());
        RegisterProcess("DatabaseMigration", DatabaseMigrationProcess.Create());
        RegisterProcess("CertificateRenewal", CertificateRenewalProcess.Create());
        RegisterProcess("MultiEnvironmentDeployment", MultiEnvDeploymentProcess.Create());
    }

    private void RegisterProcess(string name, KernelProcess process)
    {
        _processes[name] = process;
    }

    public KernelProcess GetProcess(string processName)
    {
        if (_processes.TryGetValue(processName, out var process))
        {
            return process;
        }
        throw new InvalidOperationException($"Process '{processName}' not found");
    }

    public IEnumerable<string> GetAllProcessNames() => _processes.Keys;
}
```

#### 3.2 Example: Blue/Green Deployment Process

**File**: `src/Honua.Cli.AI/Services/Processes/BlueGreenDeploymentProcess.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;

namespace Honua.Cli.AI.Services.Processes;

public static class BlueGreenDeploymentProcess
{
    public static KernelProcess Create()
    {
        var builder = new ProcessBuilder("BlueGreenDeployment");

        // Define steps
        var validateStep = builder.AddStepFromType<ValidateEnvironmentStep>();
        var deployBlueStep = builder.AddStepFromType<DeployBlueEnvironmentStep>();
        var testBlueStep = builder.AddStepFromType<TestBlueEnvironmentStep>();
        var approvalStep = builder.AddStepFromType<WaitForApprovalStep>();
        var cutoverStep = builder.AddStepFromType<CutoverTrafficStep>();
        var cleanupStep = builder.AddStepFromType<CleanupGreenEnvironmentStep>();

        // Orchestrate workflow
        builder
            .OnInputEvent("Start")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateStep));

        validateStep
            .OnEvent("ValidationPassed")
            .SendEventTo(new ProcessFunctionTargetBuilder(deployBlueStep));

        validateStep
            .OnEvent("ValidationFailed")
            .StopProcess();

        deployBlueStep
            .OnEvent("DeploymentComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(testBlueStep));

        testBlueStep
            .OnEvent("TestsPassed")
            .SendEventTo(new ProcessFunctionTargetBuilder(approvalStep));

        testBlueStep
            .OnEvent("TestsFailed")
            .StopProcess();

        approvalStep
            .OnEvent("Approved")
            .SendEventTo(new ProcessFunctionTargetBuilder(cutoverStep));

        approvalStep
            .OnEvent("Rejected")
            .StopProcess();

        cutoverStep
            .OnEvent("CutoverComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(cleanupStep));

        cleanupStep
            .OnEvent("CleanupComplete")
            .StopProcess();

        return builder.Build();
    }
}

/// <summary>
/// Stateful step that maintains deployment state.
/// </summary>
public sealed class DeployBlueEnvironmentStep : KernelProcessStep<DeploymentState>
{
    private DeploymentState _state = new();

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return base.ActivateAsync(state);
    }

    [KernelFunction]
    public async Task DeployAsync(
        string environment,
        KernelProcessStepContext context)
    {
        // Deploy blue environment (state persisted automatically)
        _state.BlueEnvironmentUrl = $"https://{environment}-blue.honua.io";
        _state.DeploymentTimestamp = DateTime.UtcNow;
        _state.Status = "deployed";

        // Emit event to next step
        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "DeploymentComplete",
            Data = _state
        });
    }
}

public sealed class DeploymentState
{
    public string BlueEnvironmentUrl { get; set; } = string.Empty;
    public string GreenEnvironmentUrl { get; set; } = string.Empty;
    public DateTime DeploymentTimestamp { get; set; }
    public string Status { get; set; } = "pending";
    public List<string> TestResults { get; set; } = new();
    public bool ApprovalGranted { get; set; }
}

// TODO: Implement remaining steps
```

---

### Phase 4: Update DI Registration

**File**: `src/Honua.Cli.AI/Extensions/AzureAIServiceCollectionExtensions.cs`

Replace existing registration:

```csharp
public static IServiceCollection AddAzureAI(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Validate configuration
    ValidateConfiguration(configuration);

    // Register LlmProviderOptions
    services.Configure<LlmProviderOptions>(configuration.GetSection("LlmProvider"));

    // Register LLM provider factory
    services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

    // Register Kernel (SK framework)
    services.AddKernel()
        .AddAzureOpenAIChatCompletion(
            configuration["LlmProvider:Azure:DeploymentName"] ?? "gpt-4",
            configuration["LlmProvider:Azure:EndpointUrl"] ?? throw new InvalidOperationException("Azure endpoint required"),
            configuration["LlmProvider:Azure:ApiKey"] ?? throw new InvalidOperationException("Azure API key required"));

    // Register SK Agent Framework components
    services.AddSingleton<IAgentRegistry, AgentRegistry>();
    services.AddSingleton<IProcessRegistry, ProcessRegistry>();

    // Register SK-based coordinator (replaces custom coordinator)
    services.AddSingleton<IAgentCoordinator, SKAgentCoordinator>();

    // Register embedding provider
    services.AddSingleton<IEmbeddingProvider>(sp =>
    {
        var options = new LlmProviderOptions();
        configuration.GetSection("LlmProvider").Bind(options);
        return new AzureOpenAIEmbeddingProvider(options);
    });

    // Register guard system (BEFORE keyed services for App Insights compatibility)
    services.AddSingleton<IInputGuard>(sp =>
    {
        var llmFactory = sp.GetRequiredService<ILlmProviderFactory>();
        var logger = sp.GetRequiredService<ILogger<LlmInputGuard>>();
        var llmProvider = llmFactory.CreateProvider();
        return new LlmInputGuard(llmProvider, logger);
    });

    services.AddSingleton<IOutputGuard>(sp =>
    {
        var llmFactory = sp.GetRequiredService<ILlmProviderFactory>();
        var logger = sp.GetRequiredService<ILogger<LlmOutputGuard>>();
        var llmProvider = llmFactory.CreateProvider();
        return new LlmOutputGuard(llmProvider, logger);
    });

    // Register Azure AI Foundry Telemetry
    services.AddHonuaAITelemetry(configuration);

    // Register knowledge store and other services (existing)
    services.AddMemoryCache(options => { options.SizeLimit = 100; });
    services.AddSingleton<IDeploymentPatternKnowledgeStore, AzureAISearchKnowledgeStore>();
    services.Decorate<IDeploymentPatternKnowledgeStore>((inner, sp) =>
    {
        var cache = sp.GetRequiredService<IMemoryCache>();
        var logger = sp.GetRequiredService<ILogger<CachedDeploymentPatternKnowledgeStore>>();
        return new CachedDeploymentPatternKnowledgeStore(inner, cache, logger);
    });

    services.AddSingleton<PatternExplainer>();
    services.AddSingleton<IPatternUsageTelemetry, PostgresPatternUsageTelemetry>();
    services.AddScoped<PatternApprovalService>();

    // Agent capabilities (still useful for telemetry)
    services.Configure<AgentCapabilityOptions>(configuration.GetSection("AgentCapabilities"));
    services.AddSingleton<AgentCapabilityRegistry>();
    services.AddSingleton<IntelligentAgentSelector>(); // Keep for fallback routing
    services.AddSingleton<IAgentCritic, PlanSafetyCritic>();
    services.AddSingleton<IAgentHistoryStore, PostgresAgentHistoryStore>();

    return services;
}
```

---

### Phase 5: Remove Legacy Code

**Files to Delete**:
- `src/Honua.Cli.AI/Services/Agents/SemanticAgentCoordinator.cs` ❌ DELETE
- `src/Honua.Cli.AI/Services/Agents/Specialized/*.cs` ❌ DELETE (replaced by AgentRegistry)

**Keep**:
- ✅ `IAgentCoordinator.cs` (interface stays)
- ✅ `AgentExecutionContext.cs` (still useful)
- ✅ `AgentCapabilityRegistry.cs` (useful for telemetry)
- ✅ Guard system (already SK-compatible)

---

## 📊 Final Architecture

```
Honua.AI
├── Services/
│   ├── Agents/
│   │   ├── SK/
│   │   │   ├── AgentRegistry.cs (replaces 20+ agent files)
│   │   │   └── SKAgentCoordinator.cs (replaces SemanticAgentCoordinator)
│   │   ├── IAgentCoordinator.cs
│   │   └── AgentExecutionContext.cs
│   ├── Processes/
│   │   ├── ProcessRegistry.cs
│   │   ├── BlueGreenDeploymentProcess.cs
│   │   ├── DatabaseMigrationProcess.cs
│   │   └── ...
│   ├── Guards/
│   │   ├── IInputGuard.cs
│   │   ├── LlmInputGuard.cs
│   │   ├── IOutputGuard.cs
│   │   └── LlmOutputGuard.cs
│   └── Observability/
│       └── OpenTelemetryConfiguration.cs
└── Extensions/
    └── AzureAIServiceCollectionExtensions.cs
```

---

## ✅ Benefits of This Approach

### 1. **Clean Architecture**
- No legacy code
- Pure SK framework patterns
- Easy to maintain

### 2. **Native SK Features**
- Built-in conversation history
- Native telemetry
- Serializable agent state
- AutoGen integration ready

### 3. **Simplified Codebase**
- 20+ agent files → 1 registry file
- Manual routing → SK AgentGroupChat
- Custom history → SK ChatHistory

### 4. **Process Framework**
- Stateful workflows with checkpointing
- Human-in-the-loop approval steps
- Resume interrupted processes

### 5. **Azure AI Foundry**
- Visualize multi-agent conversations
- Token usage analytics
- Performance monitoring

---

## 🎯 Next Steps

1. ✅ **Implement AgentRegistry** (create all 20+ agent factories)
2. ✅ **Implement SKAgentCoordinator** (complete intent parsing)
3. ✅ **Create 2-3 Process examples** (BlueGreen, DatabaseMigration, CertificateRenewal)
4. ✅ **Add OpenTelemetry config** (complete implementation)
5. ✅ **Update DI registration** (wire everything together)
6. ✅ **Delete legacy coordinator** (SemanticAgentCoordinator.cs)
7. ✅ **Delete specialized agents** (Specialized/*.cs folder)
8. ✅ **Test end-to-end** (agents, group chat, processes)

---

## 📅 Timeline

- **Day 1**: AgentRegistry + all agent factories
- **Day 2**: SKAgentCoordinator implementation
- **Day 3**: Process Framework examples
- **Day 4**: OpenTelemetry configuration
- **Day 5**: Testing & cleanup

**Total**: 5 days for complete migration

---

This is the **correct** way to build it from day one using SK framework! 🎉
