# Honua AI: Magentic Orchestration Architecture

**Date**: 2025-10-16
**Pattern**: Magentic-One (AutoGen-inspired) via Semantic Kernel
**Status**: ✅ **RECOMMENDED ARCHITECTURE**

---

## 🎯 Why Magentic Orchestration is Perfect for Honua

### Honua's Challenge
Deploying GIS infrastructure is **complex and open-ended**:
- Requirements evolve as we ask questions ("What's your budget?" → "What cloud provider?")
- Solution path isn't predetermined (Serverless? Kubernetes? VMs?)
- Requires multiple rounds of research, analysis, and refinement
- Needs dynamic collaboration between specialists

### Magentic Solution
A **Magentic manager coordinates specialized agents** that:
- Break down complex deployment requests
- Research best practices dynamically
- Generate infrastructure code iteratively
- Review cost and security continuously
- Adapt based on user feedback

**Perfect fit!** 🎯

---

## 🏗️ Architecture

### High-Level Overview

```
User Request: "Deploy Honua for 10,000 users on AWS"
            │
            ▼
┌─────────────────────────────────────────────────┐
│   StandardMagenticManager                        │
│   (Orchestrates, tracks progress, adapts)       │
└───────────────┬─────────────────────────────────┘
                │
                │ Dynamically selects agents based on context
                │
     ┌──────────┼──────────┬──────────┬──────────┐
     ▼          ▼          ▼          ▼          ▼
┌─────────┐ ┌──────────┐ ┌─────────┐ ┌────────┐ ┌──────────┐
│Research │ │Architect │ │  Coder  │ │  Cost  │ │ Security │
│ Agent   │ │  Agent   │ │  Agent  │ │ Agent  │ │  Agent   │
└─────────┘ └──────────┘ └─────────┘ └────────┘ └──────────┘
     │           │            │           │           │
     └───────────┴────────────┴───────────┴───────────┘
                          │
                          ▼
            Final deployment plan + infrastructure code
```

### Agent Roles for Honua

| Agent | Role | Capabilities |
|-------|------|--------------|
| **ResearchAgent** | Find best practices, cloud documentation | Web search (gpt-4o-search-preview) |
| **ArchitectAgent** | Design architecture, compare options | Chat completion, cost modeling |
| **CoderAgent** | Generate Terraform, K8s manifests, docker-compose | Code interpreter, validation |
| **CostReviewAgent** | Analyze costs, optimize spending | Pricing API, TCO calculation |
| **SecurityAgent** | Security hardening, compliance | Security scanning, best practices |
| **DeploymentAgent** | Execute deployment, validate | Terraform execution, health checks |

---

## 📦 Implementation

### Step 1: Add Required Packages

```bash
cd src/Honua.Cli.AI
dotnet add package Microsoft.SemanticKernel.Agents.Orchestration --prerelease
dotnet add package Microsoft.SemanticKernel.Agents.Runtime.InProcess --prerelease
```

Update `Honua.Cli.AI.csproj`:

```xml
<PackageReference Include="Microsoft.SemanticKernel.Agents.Orchestration" Version="1.66.0-alpha" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Runtime.InProcess" Version="1.66.0-alpha" />
```

---

### Step 2: Create Specialized Agents

**File**: `src/Honua.Cli.AI/Services/Agents/Magentic/MagenticAgentFactory.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.Magentic;

/// <summary>
/// Factory for creating specialized agents for Magentic orchestration.
/// Each agent has a focused role in the deployment workflow.
/// </summary>
public sealed class MagenticAgentFactory
{
    private readonly IKernelBuilder _kernelBuilder;

    public MagenticAgentFactory(IKernelBuilder kernelBuilder)
    {
        _kernelBuilder = kernelBuilder ?? throw new ArgumentNullException(nameof(kernelBuilder));
    }

    /// <summary>
    /// Research Agent: Web search for best practices, documentation, examples.
    /// Uses gpt-4o-search-preview for native web search capability.
    /// </summary>
    public ChatCompletionAgent CreateResearchAgent()
    {
        // Create kernel with search-enabled model
        var kernel = _kernelBuilder.Build();

        return new ChatCompletionAgent
        {
            Name = "ResearchAgent",
            Description = "Searches the web for GIS deployment best practices, cloud documentation, and technical examples. Ask it to find information about specific technologies.",
            Instructions = """
                You are a Research Specialist for GIS infrastructure deployments.

                Your capabilities:
                - Search the web for best practices
                - Find official cloud provider documentation
                - Locate technical examples and tutorials
                - Research pricing and cost information
                - Find security best practices

                When asked to research:
                1. Use web search to find authoritative sources
                2. Prioritize official documentation (AWS, Azure, GCP, Kubernetes)
                3. Summarize findings concisely
                4. Provide links to sources
                5. Highlight key recommendations

                Focus on: PostGIS, GeoServer, pg_tileserv, OGC services, vector tiles.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    Temperature = 0.3,
                    MaxTokens = 4000
                })
        };
    }

    /// <summary>
    /// Architecture Agent: Designs deployment architecture, compares options.
    /// </summary>
    public ChatCompletionAgent CreateArchitectAgent()
    {
        var kernel = _kernelBuilder.Build();

        return new ChatCompletionAgent
        {
            Name = "ArchitectAgent",
            Description = "Designs GIS deployment architectures. Analyzes requirements (users, data volume, traffic) and proposes multiple architecture options with pros/cons.",
            Instructions = """
                You are an Architecture Consultant for Honua GIS deployments.

                Your responsibilities:
                1. Analyze requirements (users, data volume, traffic patterns, budget)
                2. Design 2-3 architecture options:
                   - Serverless (AWS Lambda, API Gateway, Aurora Serverless)
                   - Kubernetes (EKS, AKS, GKE with autoscaling)
                   - VMs (EC2, Azure VMs with load balancers)
                   - Hybrid approaches
                3. Compare trade-offs:
                   - Cost vs. performance
                   - Operational complexity
                   - Scalability limits
                   - Vendor lock-in
                4. Recommend the best fit with rationale

                Always provide diagrams (Mermaid syntax) and pros/cons tables.
                Focus on: PostgreSQL/PostGIS, pg_tileserv, reverse proxy (nginx/Caddy).
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.7,
                    MaxTokens = 6000
                })
        };
    }

    /// <summary>
    /// Coder Agent: Generates infrastructure code, validates syntax.
    /// Uses OpenAIAssistantAgent with code interpreter for advanced capabilities.
    /// </summary>
    public OpenAIAssistantAgent CreateCoderAgent(string openAIApiKey)
    {
        return OpenAIAssistantAgent.Create(
            clientProvider: OpenAIClientProvider.ForAzureOpenAI(apiKey: openAIApiKey, endpoint: new Uri("https://...")),
            definition: new OpenAIAssistantDefinition("gpt-4-turbo")
            {
                Name = "CoderAgent",
                Description = "Generates infrastructure code (Terraform, Kubernetes manifests, docker-compose). Validates syntax and best practices.",
                Instructions = """
                    You are an Infrastructure Code Generator for Honua GIS deployments.

                    Your capabilities:
                    - Generate Terraform for AWS, Azure, GCP
                    - Create Kubernetes manifests (Deployments, Services, ConfigMaps)
                    - Write docker-compose.yml files
                    - Validate YAML/HCL syntax
                    - Add detailed comments explaining each section

                    Code standards:
                    - Use modules and reusable components
                    - Follow cloud provider best practices
                    - Include security configurations (encryption, IAM)
                    - Add health checks and monitoring
                    - Implement autoscaling where appropriate

                    Always validate code before returning.
                    """,
                EnableCodeInterpreter = true,
                Metadata = new Dictionary<string, string>
                {
                    ["agent_type"] = "coder",
                    ["specialized_in"] = "infrastructure"
                }
            });
    }

    /// <summary>
    /// Cost Review Agent: Analyzes costs, suggests optimizations.
    /// </summary>
    public ChatCompletionAgent CreateCostReviewAgent()
    {
        var kernel = _kernelBuilder.Build();

        return new ChatCompletionAgent
        {
            Name = "CostReviewAgent",
            Description = "Analyzes infrastructure costs and suggests optimizations. Calculates Total Cost of Ownership (TCO).",
            Instructions = """
                You are a Cost Optimization Specialist for GIS deployments.

                Your responsibilities:
                1. Analyze proposed infrastructure for costs
                2. Calculate monthly/annual TCO
                3. Identify expensive components:
                   - Oversized instances
                   - Expensive storage tiers
                   - Data transfer costs
                   - NAT Gateway costs
                4. Suggest alternatives:
                   - Reserved instances vs. on-demand
                   - S3 vs. EBS for static data
                   - Spot instances for batch processing
                   - Regional pricing differences
                5. Provide cost breakdown by service

                Always give specific $ estimates (monthly & annual).
                Compare "before" and "after" optimization costs.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.2,
                    MaxTokens = 3000
                })
        };
    }

    /// <summary>
    /// Security Agent: Security hardening, compliance checks.
    /// </summary>
    public ChatCompletionAgent CreateSecurityAgent()
    {
        var kernel = _kernelBuilder.Build();

        return new ChatCompletionAgent
        {
            Name = "SecurityAgent",
            Description = "Reviews security configurations and recommends hardening. Ensures compliance with best practices.",
            Instructions = """
                You are a Security Specialist for GIS infrastructure.

                Your responsibilities:
                1. Review infrastructure for security issues:
                   - Public vs. private subnets
                   - Security group rules (least privilege)
                   - Encryption at rest and in transit
                   - IAM policies (overly permissive?)
                   - Secrets management
                2. Recommend hardening:
                   - Enable AWS GuardDuty, Azure Security Center
                   - Implement WAF rules
                   - Add MFA requirements
                   - Enable audit logging
                   - Configure backup encryption
                3. Check compliance:
                   - HTTPS only (no HTTP)
                   - Database encryption
                   - Least privilege access
                   - Network segmentation

                Always provide specific security issues with severity (High/Medium/Low).
                Include remediation steps.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.1,
                    MaxTokens = 4000
                })
        };
    }

    /// <summary>
    /// Deployment Agent: Executes deployment, validates health.
    /// </summary>
    public ChatCompletionAgent CreateDeploymentAgent()
    {
        var kernel = _kernelBuilder.Build();

        // TODO: Add plugins for actual execution (Terraform, kubectl, etc.)

        return new ChatCompletionAgent
        {
            Name = "DeploymentAgent",
            Description = "Executes deployment plans. Runs terraform apply, validates infrastructure health.",
            Instructions = """
                You are a Deployment Execution Specialist.

                Your responsibilities:
                1. Execute deployment plans:
                   - terraform init / plan / apply
                   - kubectl apply -f manifests/
                   - docker-compose up -d
                2. Validate deployment:
                   - Health check endpoints
                   - Database connectivity
                   - Service discovery
                   - DNS resolution
                3. Monitor deployment progress
                4. Handle rollback if failures occur
                5. Generate deployment summary report

                Always confirm before executing destructive operations.
                Log all commands and outputs.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.1,
                    MaxTokens = 2000
                })
        };
    }
}
```

---

### Step 3: Create Magentic Coordinator

**File**: `src/Honua.Cli.AI/Services/Agents/Magentic/HonuaMagenticCoordinator.cs`

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Runtime;
using Microsoft.SemanticKernel.ChatCompletion;
using Honua.Cli.AI.Services.Guards;

namespace Honua.Cli.AI.Services.Agents.Magentic;

/// <summary>
/// Honua's Magentic orchestration coordinator.
/// Uses StandardMagenticManager to coordinate specialized agents for GIS deployments.
/// </summary>
public sealed class HonuaMagenticCoordinator : IAgentCoordinator
{
    private readonly MagenticAgentFactory _agentFactory;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IInputGuard? _inputGuard;
    private readonly IOutputGuard? _outputGuard;
    private readonly ILogger<HonuaMagenticCoordinator> _logger;
    private static readonly ActivitySource ActivitySource = new("Honua.AI.Magentic");

    public HonuaMagenticCoordinator(
        MagenticAgentFactory agentFactory,
        IChatCompletionService chatCompletion,
        ILogger<HonuaMagenticCoordinator> logger,
        IInputGuard? inputGuard = null,
        IOutputGuard? outputGuard = null)
    {
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inputGuard = inputGuard;
        _outputGuard = outputGuard;
    }

    public async Task<AgentCoordinatorResult> ProcessRequestAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Magentic.Orchestrate");
        activity?.SetTag("request", request);
        activity?.SetTag("dry_run", context.DryRun);

        var warnings = new List<string>();
        var agentsInvolved = new List<string>();

        try
        {
            // Phase 1: Input Guard
            if (_inputGuard != null)
            {
                var inputCheck = await _inputGuard.ValidateInputAsync(
                    request,
                    "Honua GIS deployment consultant",
                    cancellationToken);

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

            // Phase 2: Create specialized agents
            var agents = new Agent[]
            {
                _agentFactory.CreateResearchAgent(),
                _agentFactory.CreateArchitectAgent(),
                _agentFactory.CreateCoderAgent("your-openai-key"), // TODO: Get from config
                _agentFactory.CreateCostReviewAgent(),
                _agentFactory.CreateSecurityAgent(),
                _agentFactory.CreateDeploymentAgent()
            };

            activity?.SetTag("agents_count", agents.Length);

            // Phase 3: Create Magentic manager
            var manager = new StandardMagenticManager(_chatCompletion)
            {
                MaximumInvocationCount = 15  // Max agent interactions
            };

            // Phase 4: Create orchestration with callback
            var conversationMessages = new List<string>();

            void AgentResponseCallback(ChatMessageContent message)
            {
                var formattedMessage = $"**{message.AuthorName}**: {message.Content}";
                conversationMessages.Add(formattedMessage);
                agentsInvolved.Add(message.AuthorName ?? "unknown");

                _logger.LogInformation("Agent: {Agent}, Message: {Message}",
                    message.AuthorName, message.Content);

                // Record in activity
                activity?.AddEvent(new ActivityEvent($"agent.response.{message.AuthorName}",
                    tags: new ActivityTagsCollection { { "content_length", message.Content?.Length ?? 0 } }));
            }

            var orchestration = new MagenticOrchestration(
                members: agents,
                manager: manager,
                agentResponseCallback: AgentResponseCallback);

            // Phase 5: Start runtime and invoke orchestration
            using var runtime = new InProcessRuntime();
            await runtime.StartAsync(cancellationToken);

            _logger.LogInformation("Starting Magentic orchestration for: {Request}", request);

            var invocationTask = await orchestration.InvokeAsync(
                task: request,
                runtime: runtime,
                cancellationToken: cancellationToken);

            // Phase 6: Get final result
            var finalResult = await invocationTask.GetAsync(cancellationToken);

            await runtime.StopWhenIdleAsync(cancellationToken);

            _logger.LogInformation("Magentic orchestration completed. Agents involved: {Agents}",
                string.Join(", ", agentsInvolved.Distinct()));

            // Phase 7: Synthesize response
            var response = SynthesizeResponse(conversationMessages, finalResult);

            // Phase 8: Output Guard
            if (_outputGuard != null)
            {
                var outputCheck = await _outputGuard.ValidateOutputAsync(
                    response,
                    string.Join(",", agentsInvolved.Distinct()),
                    request,
                    cancellationToken);

                if (!outputCheck.IsSafe)
                {
                    return new AgentCoordinatorResult
                    {
                        Success = false,
                        Response = $"Agent output blocked: {outputCheck.Explanation}",
                        AgentsInvolved = agentsInvolved.Distinct().ToList(),
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
                AgentsInvolved = agentsInvolved.Distinct().ToList(),
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            _logger.LogError(ex, "Magentic orchestration failed");

            return new AgentCoordinatorResult
            {
                Success = false,
                ErrorMessage = $"Orchestration failed: {ex.Message}",
                AgentsInvolved = agentsInvolved.Distinct().ToList()
            };
        }
    }

    private string SynthesizeResponse(List<string> messages, object? finalResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Honua Deployment Plan\n");
        sb.AppendLine("Generated using Magentic multi-agent orchestration.\n");
        sb.AppendLine("---\n");

        // Agent conversation
        sb.AppendLine("## Agent Collaboration\n");
        foreach (var message in messages)
        {
            sb.AppendLine(message);
            sb.AppendLine();
        }

        // Final result
        if (finalResult != null)
        {
            sb.AppendLine("---\n");
            sb.AppendLine("## Final Result\n");
            sb.AppendLine(finalResult.ToString());
        }

        return sb.ToString();
    }

    public Task<AgentInteractionHistory> GetHistoryAsync()
    {
        throw new NotImplementedException("Magentic orchestration uses runtime conversation history");
    }
}
```

---

### Step 4: Register in DI Container

**File**: `src/Honua.Cli.AI/Extensions/AzureAIServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddAzureAI(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... existing code ...

    // Register Kernel
    services.AddKernel()
        .AddAzureOpenAIChatCompletion(
            configuration["LlmProvider:Azure:DeploymentName"] ?? "gpt-4",
            configuration["LlmProvider:Azure:EndpointUrl"] ?? throw new InvalidOperationException("Endpoint required"),
            configuration["LlmProvider:Azure:ApiKey"] ?? throw new InvalidOperationException("API key required"));

    // Register Magentic components
    services.AddSingleton<MagenticAgentFactory>();
    services.AddSingleton<IAgentCoordinator, HonuaMagenticCoordinator>();

    // ... rest of existing code ...

    return services;
}
```

---

## 🎯 Why Magentic > AgentGroupChat for Honua

| Feature | AgentGroupChat | Magentic Orchestration |
|---------|----------------|------------------------|
| **Use Case** | Predetermined workflow | Open-ended, dynamic tasks |
| **Agent Selection** | Manual strategy functions | Manager decides based on context |
| **Progress Tracking** | External | Built-in by manager |
| **Iteration** | Fixed turns | Adaptive (keeps going until complete) |
| **Context Sharing** | Manual passing | Managed by orchestrator |
| **Best For** | Structured conversations | Complex problem-solving |

**Honua's deployment scenarios = Complex problem-solving** → **Magentic wins!** 🏆

---

## 📊 Example Workflow

### User Request:
> "Deploy Honua for 10,000 concurrent users on AWS with budget of $2,000/month"

### Magentic Orchestration Flow:

1. **Manager**: "This is a complex deployment. Let me start with research."
2. **ResearchAgent**: *Searches web for AWS GIS deployment best practices*
3. **Manager**: "Good. Now let's design architectures."
4. **ArchitectAgent**: *Proposes 3 options: ECS + RDS, EKS + Aurora, Lambda + API Gateway*
5. **Manager**: "Let's analyze costs for each option."
6. **CostReviewAgent**: *Calculates TCO: Option 1 = $1,800/mo, Option 2 = $2,500/mo, Option 3 = $1,200/mo*
7. **Manager**: "Option 1 fits budget. Generate infrastructure code."
8. **CoderAgent**: *Generates Terraform for ECS + RDS + S3 + CloudFront*
9. **Manager**: "Security review needed."
10. **SecurityAgent**: *Reviews code, suggests adding WAF, encrypting RDS, enabling GuardDuty*
11. **Manager**: "Make security improvements."
12. **CoderAgent**: *Updates Terraform with security enhancements*
13. **Manager**: "Ready for deployment. Final cost check."
14. **CostReviewAgent**: *Confirms $1,850/month with security additions*
15. **Manager**: "Perfect. Here's the complete deployment plan."

**Result**: Comprehensive deployment plan created through dynamic collaboration!

---

## ✅ Next Steps

1. [ ] Add Magentic packages to project
2. [ ] Implement `MagenticAgentFactory` with all 6 agents
3. [ ] Implement `HonuaMagenticCoordinator`
4. [ ] Update DI registration
5. [ ] Test with sample deployment request
6. [ ] Add OpenTelemetry tracing
7. [ ] Remove old `SemanticAgentCoordinator`

**Timeline**: 3-4 days

---

## 📚 References

- [SK Magentic Orchestration Docs](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/magentic)
- [Magentic-One (AutoGen)](https://microsoft.github.io/autogen/blog/2024/12/20/magentic-one)
- [SK Multi-Agent Blog](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-multi-agent-orchestration/)
- [Sample Code](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStartedWithAgents/Orchestration/Step05_Magentic.cs)

---

**This is the correct architecture for Honua!** 🚀
