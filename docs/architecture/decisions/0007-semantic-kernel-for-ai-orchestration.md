# 7. Semantic Kernel for AI Orchestration

Date: 2025-10-17

Status: Accepted

## Context

Honua 2.0 introduced AI capabilities through the Honua.Cli.AI module for deployment automation, infrastructure management, and operational tasks. This requires:

- **Multi-step Workflows**: Complex processes like deployment, upgrades, certificate renewal
- **LLM Integration**: Support for OpenAI, Azure OpenAI, Anthropic, LocalAI
- **Stateful Execution**: Pause, resume, checkpoint workflow state
- **Agent Coordination**: Multiple specialized agents working together
- **Observability**: Trace AI decisions and execution flow
- **Extensibility**: Add new processes and agents easily

**Existing Evidence:**
- Semantic Kernel packages in `Honua.Cli.AI.csproj`:
  - `Microsoft.SemanticKernel` v1.66.0
  - `Microsoft.SemanticKernel.Agents.Core` v1.66.0-preview
  - `Microsoft.SemanticKernel.Process.Core` v1.66.0-alpha
  - `Microsoft.SemanticKernel.Process.LocalRuntime` v1.66.0-alpha
- Process implementations: `/src/Honua.Cli.AI/Services/Processes/`
- Agent factory: `/src/Honua.Cli.AI/Services/Agents/HonuaAgentFactory.cs`
- 8 stateful processes (Deployment, Upgrade, GitOps, Benchmark, etc.)
- 28+ specialized agents

## Decision

We will use **Microsoft Semantic Kernel** as the AI orchestration framework for Honua's AI capabilities.

**Key Components:**
- **Semantic Kernel Core**: LLM abstraction layer
- **Process Framework**: Stateful workflow engine (KernelProcessStep pattern)
- **Agents Framework**: Multi-agent coordination
- **Plugins**: Reusable functions for cloud operations, diagnostics, etc.

**Architecture:**
```
┌─────────────────────────────────────────┐
│  CLI Commands (honua process deploy)   │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│  Semantic Kernel Process Framework      │
│  - DeploymentProcess                    │
│  - UpgradeProcess                       │
│  - GitOpsProcess                        │
└──────────────────┬──────────────────────┘
                   │
         ┌─────────┼─────────┐
         ▼         ▼         ▼
    ┌────────┐ ┌────────┐ ┌────────┐
    │ Step 1 │ │ Step 2 │ │ Step 3 │
    │Validate│ │Generate│ │Deploy  │
    └────────┘ └────────┘ └────────┘
         │         │         │
         └─────────┴─────────┘
                   │
         ┌─────────▼─────────┐
         │  LLM Providers    │
         │ (OpenAI, Azure,   │
         │  Anthropic, etc.) │
         └───────────────────┘
```

## Consequences

### Positive

- **Microsoft-Backed**: Official Microsoft AI framework for .NET
- **Multi-Provider**: Supports all major LLM providers
- **Stateful Workflows**: Process Framework enables pause/resume
- **Event-Driven**: Clean step-to-step communication
- **Testable**: Mock LLM responses for testing
- **Observability**: OpenTelemetry integration
- **Active Development**: Rapid feature additions
- **Growing Ecosystem**: Community plugins and extensions

### Negative

- **Alpha/Preview**: Process Framework is pre-release (v1.66.0-alpha)
- **Breaking Changes**: API unstable, frequent updates needed
- **Learning Curve**: Complex abstractions (processes, agents, plugins)
- **Limited Documentation**: Preview features poorly documented
- **Memory Overhead**: Large context windows consume memory

### Neutral

- Requires multiple NuGet package references
- Process state serialization format may change
- Agent coordination patterns still evolving

## Alternatives Considered

### 1. LangChain .NET

**Pros:** Popular Python framework ported to .NET, large community

**Cons:** Weaker .NET support, Python-first, less official backing

**Verdict:** Rejected - Semantic Kernel better .NET integration

### 2. Custom LLM Orchestration

**Pros:** Full control, minimal dependencies

**Cons:** Reinventing the wheel, no stateful workflow support

**Verdict:** Rejected - too much custom code

### 3. Autogen / CrewAI

**Pros:** Advanced multi-agent patterns

**Cons:** Python-only, no .NET support

**Verdict:** Rejected - not available for .NET

## Implementation Example

```csharp
// Process definition
public class DeploymentProcess : KernelProcess
{
    public static ProcessBuilder CreateProcess()
    {
        var builder = new ProcessBuilder("Deployment");

        var validate = builder.AddStepFromType<ValidateRequirementsStep>();
        var generate = builder.AddStepFromType<GenerateInfrastructureStep>();
        var deploy = builder.AddStepFromType<DeployInfrastructureStep>();

        builder.OnInputEvent("Start")
            .SendEventTo(validate.WhereInputEventIs("Validate"));

        validate.OnEvent("Valid")
            .SendEventTo(generate.WhereInputEventIs("Generate"));

        generate.OnEvent("Generated")
            .SendEventTo(deploy.WhereInputEventIs("Deploy"));

        return builder;
    }
}

// Process step
public class ValidateRequirementsStep : KernelProcessStep<DeploymentState>
{
    [KernelFunction]
    public async Task ValidateAsync(KernelProcessStepContext context)
    {
        // Validation logic with LLM assistance
        await context.EmitEventAsync("Valid", data);
    }
}
```

## Code References

- Processes: `/src/Honua.Cli.AI/Services/Processes/`
- Agents: `/src/Honua.Cli.AI/Services/Agents/`
- Design Doc: `/docs/process-framework-design.md`
- Implementation Guide: `/docs/process-framework-implementation-guide.md`

## References

- [Semantic Kernel GitHub](https://github.com/microsoft/semantic-kernel)
- [Process Framework Docs](https://learn.microsoft.com/en-us/semantic-kernel/concepts/kernel-concepts/process)

## Notes

Semantic Kernel was chosen despite alpha status due to unique stateful workflow capabilities. We accept the risk of API changes for the benefits of process orchestration.
