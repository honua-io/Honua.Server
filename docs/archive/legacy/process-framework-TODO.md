# Semantic Kernel Process Framework - Implementation TODO

## Current Status

❌ **Implementation Removed** - The initial implementation had incorrect state management patterns
✅ **Design Complete** - All 20 workflows fully designed with step-by-step flows
✅ **State Classes Complete** - 5 state classes in `Services/Processes/State/`
✅ **Documentation Complete** - Comprehensive design docs and implementation guide

## Why Implementation Was Removed

The initial implementation used incorrect state access patterns (`State!.Property`) that don't match the SK Process Framework alpha API. The correct pattern is:

```csharp
public class MyStep : KernelProcessStep<MyState>
{
    // State property provided by base class

    public override ValueTask ActivateAsync(KernelProcessStepState<MyState> state)
    {
        // Load persisted state
        State = state.State;
        return ValueTask.CompletedTask;
    }

    [KernelFunction]
    public async Task ExecuteAsync(KernelProcessStepContext context, MyRequest request)
    {
        // Access state directly
        State.PropertyName = value;

        // Emit events
        await context.EmitEventAsync("EventName", data);
    }
}
```

## What Needs To Be Done

### Phase 1: Core API Research (CRITICAL)

1. **Verify SK Process Framework v1.66.0-alpha API**
   - Check if `KernelProcessStep<TState>` has a `State` property
   - Verify `ActivateAsync(KernelProcessStepState<TState>)` method signature
   - Confirm event emission API: `context.EmitEventAsync()`
   - Test process builder API: `ProcessBuilder.AddStepFromType<TStep>()`

2. **Create minimal working example**
   - 2-step process (Step1 → Step2)
   - State passed between steps
   - Verify compilation and execution

### Phase 2: Implement Deployment Process (Priority 1)

**Steps** (8 total):
1. `ValidateDeploymentRequirementsStep` - Validate cloud provider, region, credentials
2. `GenerateInfrastructureCodeStep` - Generate Terraform for AWS/Azure/GCP
3. `ReviewInfrastructureStep` - Present to user, await approval
4. `DeployInfrastructureStep` - Execute `terraform apply`
5. `ConfigureServicesStep` - Install PostGIS, configure S3, DNS
6. `DeployHonuaApplicationStep` - Deploy containers to ECS/K8s
7. `ValidateDeploymentStep` - Health checks, OGC endpoint validation
8. `ConfigureObservabilityStep` - Deploy Prometheus, Grafana

**State**: `DeploymentState.cs` (already exists)

**Process Builder**: Wire steps with event routing

### Phase 3: Implement Upgrade Process (Priority 2)

**Steps** (4 total):
1. `DetectCurrentVersionStep` - Detect running version
2. `BackupDatabaseStep` - Create DB backup to S3
3. `CreateBlueEnvironmentStep` - Deploy new version to blue environment
4. `SwitchTrafficStep` - Gradual cutover (10% → 50% → 100%)

**State**: `UpgradeState.cs` (already exists)

### Phase 4: Implement Remaining 3 Processes

- Metadata Process (3 steps)
- GitOps Config Process (3 steps)
- Benchmarking Process (4 steps)

### Phase 5: DI Registration

Register all steps in `AzureAIServiceCollectionExtensions.cs`:

```csharp
services.AddTransient<ValidateDeploymentRequirementsStep>();
services.AddTransient<GenerateInfrastructureCodeStep>();
// ... etc
```

### Phase 6: Integration with Agent Coordinator

Add process invocation logic to `SemanticAgentCoordinator`:

```csharp
if (intent.RequiresLongRunningWorkflow)
{
    var processType = intent.PrimaryIntent switch
    {
        "deployment" => DeploymentProcess.BuildProcess(),
        "upgrade" => UpgradeProcess.BuildProcess(),
        _ => null
    };

    if (processType != null)
    {
        var process = processType.Build();
        var context = await process.StartAsync(kernel, new KernelProcessEvent { ... });
        return new AgentCoordinatorResult { ProcessId = context.Id };
    }
}
```

### Phase 7: CLI Commands

```bash
honua process deploy --provider aws --region us-west-2 --tier production
honua process upgrade --deployment honua-prod --to 2.0.0
honua process status <process-id>
honua process pause <process-id>
honua process resume <process-id>
```

## Correct Implementation Pattern

### Step Implementation

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

public class ValidateDeploymentRequirementsStep : KernelProcessStep<DeploymentState>
{
    private readonly ILogger<ValidateDeploymentRequirementsStep> _logger;

    public ValidateDeploymentRequirementsStep(ILogger<ValidateDeploymentRequirementsStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        // Load persisted state (called when step is activated)
        State = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ValidateRequirements")]
    public async Task ValidateRequirementsAsync(
        KernelProcessStepContext context,
        DeploymentRequest request)
    {
        _logger.LogInformation("Validating deployment for {CloudProvider}",
            request.CloudProvider);

        // Update state
        State.DeploymentId = Guid.NewGuid().ToString();
        State.CloudProvider = request.CloudProvider;
        State.Region = request.Region;
        State.Status = "Validating";

        // Validation logic
        var supported = new[] { "AWS", "Azure", "GCP" };
        if (!supported.Contains(request.CloudProvider))
        {
            await context.EmitEventAsync("RequirementsInvalid",
                new { Error = "Unsupported provider" });
            return;
        }

        _logger.LogInformation("Requirements valid for {DeploymentId}",
            State.DeploymentId);

        // Emit event to trigger next step
        await context.EmitEventAsync("RequirementsValid", State);
    }
}
```

### Process Builder

```csharp
public static class DeploymentProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("HonuaDeployment");

        // Add steps
        var validateStep = builder.AddStepFromType<ValidateDeploymentRequirementsStep>();
        var generateStep = builder.AddStepFromType<GenerateInfrastructureCodeStep>();
        var reviewStep = builder.AddStepFromType<ReviewInfrastructureStep>();
        // ... add remaining steps

        // Wire event routing
        builder
            .OnInputEvent("StartDeployment")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateStep, "ValidateRequirements"));

        validateStep
            .OnFunctionResult("ValidateRequirements")
            .SendEventTo(new ProcessFunctionTargetBuilder(generateStep, "GenerateInfrastructure"));

        // ... wire remaining steps

        return builder;
    }
}
```

## Testing Strategy

1. **Unit Tests** - Test each step in isolation
2. **Integration Tests** - Test full process end-to-end
3. **State Persistence Tests** - Verify pause/resume works
4. **Failure Tests** - Verify rollback triggers on step failure

## Resources

- **State Classes**: `/home/mike/projects/HonuaIO/src/Honua.Cli.AI/Services/Processes/State/*.cs`
- **Design Docs**:
  - `docs/process-framework-design.md` (5 core workflows)
  - `docs/additional-process-workflows.md` (10 additional)
  - `docs/deployment-modification-workflows.md` (5 modifications)
- **Implementation Guide**: `docs/process-framework-implementation-guide.md`
- **Microsoft Docs**: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/process

## Next Immediate Action

**Create a minimal 2-step process to validate the API**:

1. Create `TestStep1` and `TestStep2`
2. Pass simple state between them
3. Verify compilation and execution
4. Once working, use as template for all 20 workflows

## Estimated Effort

- Phase 1 (API Research + Minimal Example): 2 hours
- Phase 2 (Deployment Process): 4 hours
- Phase 3 (Upgrade Process): 3 hours
- Phase 4 (Remaining 3 Processes): 4 hours
- Phase 5 (DI Registration): 1 hour
- Phase 6 (Agent Integration): 2 hours
- Phase 7 (CLI Commands): 3 hours

**Total**: ~19 hours of focused implementation

## Blocker Resolution

The main blocker was incorrect state management patterns. The correct pattern is now documented above. Once a minimal working example is created and verified, the remaining 20 workflows can be implemented rapidly using the same pattern.
