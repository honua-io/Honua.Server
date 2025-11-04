# Semantic Kernel Agent Framework Migration Plan

**Date**: 2025-10-16
**Current SK Version**: 1.65.0
**Target SK Version**: 1.65.0+ (with Agents & Process packages)
**Status**: Planning Phase

---

## Executive Summary

Migrate Honua's custom multi-agent system to use Microsoft's official **Semantic Kernel Agent Framework** and **Process Framework**. This aligns with Microsoft's 2025 best practices and prepares for future AutoGen integration.

### Why Migrate?

1. **Production-Grade Stability**: SK Agent Framework is GA (Q1 2025) with stable, versioned APIs
2. **Built-in Capabilities**: Native conversation history, message passing, serialization
3. **AutoGen Integration**: Seamless path to advanced multi-agent patterns
4. **Process Framework**: Stateful, long-running workflows with checkpointing
5. **Future-Proofing**: Microsoft's official direction for agentic systems

---

## Current Architecture vs. Target Architecture

### Current (Custom Implementation)

```
┌─────────────────────────────────────────┐
│     SemanticAgentCoordinator            │
│  - Custom agent routing                 │
│  - Manual conversation history          │
│  - Switch-based agent instantiation     │
└──────────────┬──────────────────────────┘
               │
               ▼
     ┌─────────────────────┐
     │ Specialized Agents  │
     │ (20+ custom classes)│
     │ - No common base    │
     │ - Manual history    │
     └─────────────────────┘
```

### Target (SK Agent Framework)

```
┌─────────────────────────────────────────┐
│      AgentGroupChat                     │
│  - KernelFunctionSelectionStrategy      │
│  - KernelFunctionTerminationStrategy    │
│  - Built-in conversation management     │
└──────────────┬──────────────────────────┘
               │
               ▼
     ┌─────────────────────────────┐
     │ ChatCompletionAgent[...]    │
     │ (Inherit from base class)   │
     │ - ChatHistory built-in      │
     │ - Plugin support native     │
     │ - Serializable              │
     └─────────────────────────────┘
```

---

## Migration Phases

### Phase 1: Package Installation & Setup ✅ **READY TO START**

**Estimated Time**: 1 day

#### 1.1 Add NuGet Packages

```bash
cd src/Honua.Cli.AI
dotnet add package Microsoft.SemanticKernel.Agents --version 1.65.0
dotnet add package Microsoft.SemanticKernel.Agents.Core --version 1.65.0
dotnet add package Microsoft.SemanticKernel.Process.Core --version 1.65.0
```

#### 1.2 Verify Package Compatibility

- ✅ Microsoft.SemanticKernel 1.65.0 (already installed)
- ➕ Microsoft.SemanticKernel.Agents 1.65.0 (add)
- ➕ Microsoft.SemanticKernel.Process.Core 1.65.0 (add)

**Files to Modify**:
- `src/Honua.Cli.AI/Honua.Cli.AI.csproj`

---

### Phase 2: Create Proof-of-Concept Examples 🎯 **CRITICAL**

**Estimated Time**: 3 days

#### 2.1 Example 1: Simple ChatCompletionAgent

**Goal**: Migrate one agent to ChatCompletionAgent pattern

**Target Agent**: `DeploymentConfigurationAgent` (simple, no complex dependencies)

**New File**: `src/Honua.Cli.AI/Services/Agents/SKAgents/DeploymentConfigurationChatAgent.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.SKAgents;

/// <summary>
/// SK Agent Framework implementation of Deployment Configuration Agent.
/// Generates infrastructure code (Terraform, docker-compose, K8s manifests).
/// </summary>
public sealed class DeploymentConfigurationChatAgent
{
    private readonly ChatCompletionAgent _agent;

    public DeploymentConfigurationChatAgent(Kernel kernel)
    {
        _agent = new ChatCompletionAgent()
        {
            Name = "DeploymentConfiguration",
            Instructions = """
                You are the Deployment Configuration specialist. You handle:
                - Infrastructure as Code generation (Terraform, docker-compose, Kubernetes)
                - Cloud provider configuration (AWS, Azure, GCP)
                - Service configuration (WFS, WMS, OGC API Features)
                - GitOps workflows for environment promotion

                Focus on declarative, version-controlled configuration changes.
                Always include comments explaining each configuration section.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    Temperature = 0.3,
                    MaxTokens = 4000
                })
        };
    }

    public async Task<string> GenerateConfigurationAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Create agent thread for conversation
        var thread = new AgentThread();

        // Invoke agent
        await foreach (var message in _agent.InvokeAsync(thread, request, cancellationToken))
        {
            if (message.Role == AuthorRole.Assistant)
            {
                return message.Content ?? "No response generated";
            }
        }

        return "No response generated";
    }
}
```

#### 2.2 Example 2: AgentGroupChat with Multiple Agents

**Goal**: Demonstrate multi-agent coordination

**Scenario**: Architecture consultation requiring ArchitectureConsulting + CostReview agents

**New File**: `src/Honua.Cli.AI/Services/Agents/SKAgents/ArchitectureConsultationGroupChat.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.SKAgents;

/// <summary>
/// Multi-agent consultation using SK AgentGroupChat.
/// Coordinates Architecture + Cost Review agents.
/// </summary>
public sealed class ArchitectureConsultationGroupChat
{
    private readonly AgentGroupChat _chat;
    private readonly ChatCompletionAgent _architectAgent;
    private readonly ChatCompletionAgent _costAgent;

    public ArchitectureConsultationGroupChat(Kernel kernel)
    {
        // Create Architecture Agent
        _architectAgent = new ChatCompletionAgent()
        {
            Name = "ArchitectureConsultant",
            Instructions = """
                You analyze requirements and present multiple architecture options.
                Focus on: scalability, cost vs. performance, operational complexity.
                Recommend specific cloud services and deployment strategies.
                """,
            Kernel = kernel
        };

        // Create Cost Review Agent
        _costAgent = new ChatCompletionAgent()
        {
            Name = "CostReviewer",
            Instructions = """
                You analyze architecture proposals for cost efficiency.
                Identify expensive services, suggest alternatives, calculate TCO.
                Always provide cost breakdowns by service.
                """,
            Kernel = kernel
        };

        // Create selection strategy
        var selectionFunction = AgentGroupChat.CreatePromptFunctionForStrategy(
            """
            Examine the RESPONSE and choose the next participant.

            If the user request needs architecture analysis, choose: ArchitectureConsultant
            If an architecture proposal exists and needs cost review, choose: CostReviewer
            If cost review is complete, choose: ArchitectureConsultant

            RESPONSE:
            {{$lastmessage}}

            Return only the agent name.
            """,
            safeParameterNames: "lastmessage");

        // Create termination strategy
        var terminationFunction = AgentGroupChat.CreatePromptFunctionForStrategy(
            """
            Examine the RESPONSE and determine if consultation is complete.

            Complete when:
            - Architecture options presented
            - Cost analysis provided
            - Final recommendation made

            RESPONSE:
            {{$lastmessage}}

            Return "yes" if complete, "no" otherwise.
            """,
            safeParameterNames: "lastmessage");

        // Create group chat
        _chat = new AgentGroupChat(_architectAgent, _costAgent)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                {
                    InitialAgent = _architectAgent,
                    ResultParser = (result) => result.GetValue<string>() ?? _architectAgent.Name
                },
                TerminationStrategy = new KernelFunctionTerminationStrategy(terminationFunction, kernel)
                {
                    MaximumIterations = 10,
                    ResultParser = (result) => result.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false
                }
            }
        };
    }

    public async Task<string> ConsultAsync(
        string userRequest,
        CancellationToken cancellationToken = default)
    {
        var responses = new List<string>();

        // Invoke group chat
        await foreach (var message in _chat.InvokeAsync(userRequest, cancellationToken))
        {
            responses.Add($"[{message.AuthorName}]: {message.Content}");
        }

        return string.Join("\n\n", responses);
    }
}
```

#### 2.3 Example 3: Process Framework for Deployment Workflow

**Goal**: Demonstrate stateful, long-running workflows

**Scenario**: Blue/Green deployment with approval checkpoints

**New File**: `src/Honua.Cli.AI/Services/Processes/BlueGreenDeploymentProcess.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Stateful blue/green deployment process with approval checkpoints.
/// </summary>
public sealed class BlueGreenDeploymentProcess
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
        var cleanupStep = builder.AddStepFromType<CleanupGreenStep>();

        // Orchestrate workflow
        builder
            .OnInputEvent("Start")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateStep));

        validateStep
            .OnEvent("ValidationPassed")
            .SendEventTo(new ProcessFunctionTargetBuilder(deployBlueStep));

        deployBlueStep
            .OnEvent("DeploymentComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(testBlueStep));

        testBlueStep
            .OnEvent("TestsPassed")
            .SendEventTo(new ProcessFunctionTargetBuilder(approvalStep));

        approvalStep
            .OnEvent("Approved")
            .SendEventTo(new ProcessFunctionTargetBuilder(cutoverStep));

        cutoverStep
            .OnEvent("CutoverComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(cleanupStep));

        return builder.Build();
    }
}

/// <summary>
/// Stateful step that maintains deployment state across invocations.
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
    public async Task DeployAsync(string environment, KernelProcessStepContext context)
    {
        // Deploy blue environment (persist state)
        _state.BlueEnvironment = $"{environment}-blue";
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
    public string BlueEnvironment { get; set; } = string.Empty;
    public string GreenEnvironment { get; set; } = string.Empty;
    public DateTime DeploymentTimestamp { get; set; }
    public string Status { get; set; } = "pending";
    public List<string> TestResults { get; set; } = new();
}

// Additional steps: ValidateEnvironmentStep, TestBlueEnvironmentStep,
// WaitForApprovalStep, CutoverTrafficStep, CleanupGreenStep...
```

**Files to Create**:
- `src/Honua.Cli.AI/Services/Agents/SKAgents/DeploymentConfigurationChatAgent.cs`
- `src/Honua.Cli.AI/Services/Agents/SKAgents/ArchitectureConsultationGroupChat.cs`
- `src/Honua.Cli.AI/Services/Processes/BlueGreenDeploymentProcess.cs`
- `tests/Honua.Cli.AI.Tests/SKAgents/DeploymentConfigurationChatAgentTests.cs`

---

### Phase 3: Parallel Implementation Strategy 🔄 **RECOMMENDED**

**Estimated Time**: 2 weeks

#### 3.1 Keep Existing System Running

- ✅ Do NOT modify `SemanticAgentCoordinator.cs` yet
- ✅ Existing agents continue working
- ✅ Zero production disruption

#### 3.2 Create New SK-Based System Alongside

**New Namespace**: `Honua.Cli.AI.Services.Agents.SKAgents`

**Structure**:
```
src/Honua.Cli.AI/Services/Agents/
├── Specialized/              # Existing (keep)
│   ├── DeploymentConfigurationAgent.cs
│   ├── ArchitectureConsultingAgent.cs
│   └── ...
├── SKAgents/                 # New (SK Agent Framework)
│   ├── DeploymentConfigurationChatAgent.cs
│   ├── ArchitectureConsultingChatAgent.cs
│   └── ...
├── SemanticAgentCoordinator.cs  # Existing (keep)
└── SKAgentGroupCoordinator.cs   # New (SK-based)
```

#### 3.3 Feature Flag for Gradual Rollout

```csharp
// appsettings.json
{
  "AgentFramework": {
    "UseSKAgents": false,  // Toggle SK Agent Framework on/off
    "EnabledSKAgents": [   // Gradual migration per agent
      "DeploymentConfiguration"
    ]
  }
}
```

---

### Phase 4: Full Migration Roadmap 📅

#### 4.1 Agent Priority (High → Low)

**High Priority** (Migrate First):
1. `DeploymentConfigurationAgent` - Simple, stateless
2. `ArchitectureConsultingAgent` - Good for multi-agent patterns
3. `CostReviewAgent` - Works well with ArchitectureConsulting
4. `SecurityReviewAgent` - Pairs with DeploymentConfiguration

**Medium Priority**:
5. `TroubleshootingAgent`
6. `PerformanceOptimizationAgent`
7. `BlueGreenDeploymentAgent` - Process Framework candidate

**Low Priority**:
8. Complex agents with heavy dependencies
9. Agents requiring stateful workflows (migrate to Process Framework)

#### 4.2 Process Framework Candidates

Workflows that benefit from stateful, long-running processes:

1. **Blue/Green Deployment**
   - Steps: Validate → Deploy Blue → Test → Approval → Cutover → Cleanup
   - State: Environment URLs, test results, approval timestamp

2. **Database Migration**
   - Steps: Backup → Schema Migration → Data Migration → Validation → Rollback Plan
   - State: Backup location, migration scripts, row counts

3. **Certificate Renewal**
   - Steps: Generate CSR → Submit → Wait for Issuance → Install → Verify
   - State: Certificate ID, expiration date, renewal status

4. **Multi-Day Deployment**
   - Steps: Plan → Approve → Deploy Dev → Test → Deploy Staging → Approve → Deploy Prod
   - State: Environment statuses, approval records, rollback points

---

## Migration Checklist

### Phase 1: Setup ✅
- [ ] Add `Microsoft.SemanticKernel.Agents` package
- [ ] Add `Microsoft.SemanticKernel.Process.Core` package
- [ ] Verify package compatibility
- [ ] Update documentation

### Phase 2: POC ✅
- [ ] Create `DeploymentConfigurationChatAgent` example
- [ ] Create `ArchitectureConsultationGroupChat` example
- [ ] Create `BlueGreenDeploymentProcess` example
- [ ] Write unit tests for examples
- [ ] Integration test with LocalAI

### Phase 3: Parallel Implementation ✅
- [ ] Create `SKAgents/` namespace
- [ ] Implement feature flag system
- [ ] Create `SKAgentGroupCoordinator`
- [ ] Add DI registration for SK agents
- [ ] Update telemetry for SK agents

### Phase 4: Full Migration ✅
- [ ] Migrate high-priority agents (4 agents)
- [ ] Migrate medium-priority agents (3 agents)
- [ ] Migrate low-priority agents
- [ ] Implement Process Framework workflows (3+ workflows)
- [ ] Update all tests
- [ ] Update documentation
- [ ] Remove legacy coordinator (when 100% migrated)

---

## Benefits of Migration

### Immediate Benefits

1. **Standard API**: Use official Microsoft patterns
2. **Conversation Management**: Built-in `ChatHistory` handling
3. **Serialization**: Save/restore agent state
4. **Message Routing**: Native agent-to-agent communication
5. **AutoGen Integration**: Future-proof for advanced patterns

### Long-Term Benefits

1. **Stateful Workflows**: Process Framework for multi-day deployments
2. **Checkpointing**: Resume interrupted workflows
3. **Human-in-the-Loop**: Built-in approval steps
4. **Observability**: Better monitoring and debugging
5. **Community Support**: Official Microsoft documentation and samples

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking changes in SK API | High | Use stable 1.65.0+, monitor release notes |
| Performance regression | Medium | Benchmark SK agents vs. custom agents |
| Feature gaps in SK framework | Medium | Keep custom implementation as fallback |
| Developer learning curve | Low | Provide examples, documentation |
| Production disruption | Critical | Parallel implementation with feature flags |

---

## Success Criteria

### Phase 2 (POC) Complete When:
- ✅ 3 working SK agent examples
- ✅ Unit tests passing
- ✅ Integration tests with LocalAI passing
- ✅ Performance comparable to custom implementation

### Phase 3 (Parallel) Complete When:
- ✅ Feature flag system working
- ✅ 4 high-priority agents migrated
- ✅ Multi-agent coordination working
- ✅ No production issues

### Phase 4 (Full Migration) Complete When:
- ✅ All agents migrated to SK framework
- ✅ 3+ Process Framework workflows implemented
- ✅ Legacy coordinator removed
- ✅ Documentation updated
- ✅ Team trained on SK patterns

---

## Timeline Estimate

| Phase | Duration | Start | End |
|-------|----------|-------|-----|
| Phase 1: Setup | 1 day | Day 1 | Day 1 |
| Phase 2: POC | 3 days | Day 2 | Day 4 |
| Phase 3: Parallel | 2 weeks | Day 5 | Day 18 |
| Phase 4: Full Migration | 4 weeks | Day 19 | Day 46 |

**Total Estimated Time**: ~7 weeks

---

## Next Steps

1. **Review & Approval**: Get stakeholder buy-in
2. **Phase 1 Start**: Add NuGet packages
3. **Phase 2 POC**: Create 3 examples
4. **Phase 2 Review**: Evaluate performance and developer experience
5. **Go/No-Go Decision**: Proceed with full migration or adjust

---

## References

- [SK Agent Framework Docs](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/)
- [SK Process Framework Docs](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process/)
- [SK Agent Examples](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/Agents)
- [SK Process Examples](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/Process)
- [SK Roadmap 2025](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-roadmap-h1-2025-accelerating-agents-processes-and-integration/)
