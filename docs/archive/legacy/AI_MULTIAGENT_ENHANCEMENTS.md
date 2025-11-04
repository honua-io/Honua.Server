# Multi-Agent Pattern Enhancements

## Overview

Enhanced Honua AI with advanced multi-agent patterns based on Google Cloud's agentic AI design patterns:

1. **Review & Critique Pattern**: Security and cost review agents
2. **Hierarchical Task Decomposition**: Complex task breakdown
3. **Loop Pattern**: Validation with automatic retry/remediation
4. **Swarm Pattern**: Architecture consensus through agent collaboration
5. **Multi-Provider LLM**: Second opinions and consensus across Anthropic/OpenAI

## Architecture

```
User Request
     ↓
SemanticAgentCoordinator (Enhanced)
     ↓
     ├─ Intent Analysis (LLM)
     ├─ Hierarchical Decomposition (if complex)
     │      ↓
     │   TaskDecomposition → Phases → Parallel/Sequential Tasks
     ↓
Multi-Agent Execution
     ├─ Architecture Swarm (for design decisions)
     │      ↓
     │   CostOptimizer + PerformanceOptimizer + SimplicityAdvocate + ScalabilityArchitect
     │      ↓
     │   Debate → Critique → Consensus → Track User Selection
     │
     ├─ Specialized Agents (DeploymentConfig, Security, etc.)
     │      ↓
     │   Generate Artifacts (Terraform, K8s, Docker)
     │      ↓
     │   Review & Critique Loop
     │      ├─ SecurityReviewAgent (heuristic + LLM)
     │      └─ CostReviewAgent (heuristic + LLM)
     │             ↓
     │          Approved? → Execute : Revise
     │
     └─ Validation Loop
            ↓
         Execute → Validate → Success? : Remediate → Retry
```

## New Components

### 1. SecurityReviewAgent (`src/Honua.Cli.AI/Services/Agents/Specialized/SecurityReviewAgent.cs`)

**Purpose**: Reviews infrastructure code for security vulnerabilities.

**How it works**:
- Fast heuristic checks (regex patterns for hardcoded credentials, public access, etc.)
- LLM-powered deep analysis for complex security issues
- Returns `SecurityReviewResult` with severity-ranked issues

**Usage**:
```csharp
var securityReviewer = new SecurityReviewAgent(kernel, llmProvider);
var result = await securityReviewer.ReviewAsync(
    "terraform",
    terraformContent,
    context,
    cancellationToken);

if (!result.Approved)
{
    // Critical/high severity issues found
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"[{issue.Severity}] {issue.Category}: {issue.Description}");
        Console.WriteLine($"  Fix: {issue.Recommendation}");
    }
}
```

**Checks**:
- Hardcoded credentials (critical)
- Missing encryption at rest (high)
- Public database access (high)
- Overly permissive security groups (high)
- Privileged containers (high)
- Missing TLS enforcement (medium)
- Missing resource limits (medium)

### 2. CostReviewAgent (`src/Honua.Cli.AI/Services/Agents/Specialized/CostReviewAgent.cs`)

**Purpose**: Analyzes infrastructure for cost optimization opportunities.

**How it works**:
- Heuristic checks for known expensive patterns
- LLM analysis for complex cost optimization
- Estimates monthly savings per issue

**Usage**:
```csharp
var costReviewer = new CostReviewAgent(kernel, llmProvider);
var result = await costReviewer.ReviewAsync(
    "terraform",
    terraformContent,
    context,
    cancellationToken);

Console.WriteLine($"Estimated monthly savings: ${result.EstimatedMonthlySavings}");
foreach (var issue in result.Issues)
{
    Console.WriteLine($"[{issue.Impact}] {issue.Category}: ${issue.EstimatedMonthlySavingsUsd}/month");
    Console.WriteLine($"  {issue.Recommendation}");
}
```

**Checks**:
- Oversized RDS instances ($1200/month savings)
- Missing auto-scaling ($400/month)
- Multiple NAT Gateways ($200/month each)
- Provisioned IOPS vs gp3 ($500/month)
- Premium storage vs standard ($250/month)
- Missing S3 lifecycle policies ($200/month)
- Missing K8s HPA ($400/month)

### 3. HierarchicalTaskDecomposer (`src/Honua.Cli.AI/Services/Agents/HierarchicalTaskDecomposer.cs`)

**Purpose**: Breaks complex tasks into hierarchical phases with parallel/sequential execution.

**When to use**:
- Multi-cloud deployments (deploy to AWS + Azure + GCP)
- Complex workflows (4+ agents needed)
- High-complexity keywords (multi-region, blue-green, microservices)

**Usage**:
```csharp
var decomposer = new HierarchicalTaskDecomposer(llmProvider, logger);

var decision = await decomposer.ShouldDecomposeAsync(
    request, intent, context, cancellationToken);

if (decision.ShouldDecompose)
{
    var decomposition = await decomposer.DecomposeAsync(
        request, intent, decision.DecompositionStrategy, context, cancellationToken);

    foreach (var phase in decomposition.Phases)
    {
        Console.WriteLine($"Phase: {phase.Name} (parallel: {phase.Parallelizable})");
        foreach (var task in phase.Tasks)
        {
            Console.WriteLine($"  - {task.Name} via {task.Agent}");
        }
    }
}
```

**Strategies**:
- `ParallelByCloudProvider`: Deploy to multiple clouds simultaneously
- `SequentialWithSubtasks`: Plan → Execute → Validate pipeline
- `MixedParallelSequential`: Hybrid approach

### 4. ValidationLoopExecutor (`src/Honua.Cli.AI/Services/Agents/ValidationLoopExecutor.cs`)

**Purpose**: Executes actions with validation and automatic retry/remediation.

**Usage**:
```csharp
var loopExecutor = new ValidationLoopExecutor(logger);

var result = await loopExecutor.ExecuteWithValidationAsync(
    // Execute
    async ct => await deploymentAgent.DeployAsync(config, ct),

    // Validate
    async (deployResult, ct) => await validationAgent.ValidateDeploymentAsync(deployResult, ct),

    // Remediate if validation fails
    async (validationResult, ct) => await troubleshootingAgent.RemediateAsync(validationResult, ct),

    "AWS Deployment",
    cancellationToken
);

Console.WriteLine($"Completed in {result.TotalIterations} iterations");
foreach (var iteration in result.Iterations)
{
    Console.WriteLine($"  Attempt {iteration.Attempt}: {iteration.Phase}");
}
```

**Features**:
- Max 3 retries by default
- Automatic remediation between attempts
- Exponential backoff
- Detailed iteration tracking

### 5. ArchitectureSwarmCoordinator (`src/Honua.Cli.AI/Services/Agents/ArchitectureSwarmCoordinator.cs`)

**Purpose**: Generates architecture options through collaborative agent swarm.

**Swarm Agents**:
- **CostOptimizer**: Minimizes infrastructure cost
- **PerformanceOptimizer**: Maximizes performance/latency
- **SimplicityAdvocate**: Minimizes operational complexity
- **ScalabilityArchitect**: Designs for massive scale

**How it works**:
1. **Round 1**: Each agent proposes architecture from their perspective
2. **Round 2**: Agents critique each other's proposals
3. **Round 3**: Synthesize consensus with tradeoff analysis

**Usage**:
```csharp
var swarm = new ArchitectureSwarmCoordinator(llmProvider, logger, telemetry);

var consensus = await swarm.GenerateArchitectureOptionsAsync(
    "Deploy Honua for 10k concurrent users",
    context,
    cancellationToken
);

Console.WriteLine("Consensus items:");
foreach (var item in consensus.ConsensusItems)
{
    Console.WriteLine($"  ✓ {item}");
}

Console.WriteLine("\nOptions:");
foreach (var option in consensus.Options)
{
    Console.WriteLine($"  {option.Name}: {option.Approach}");
    Console.WriteLine($"    Cost: ${option.EstimatedCostPerMonth}/mo");
    Console.WriteLine($"    Complexity: {option.ComplexityScore}/10");
    Console.WriteLine($"    Best for: {option.BestFor}");
}

// Track user selection for learning loop
await swarm.TrackUserSelectionAsync("CostOptimizer", consensus, request, cancellationToken);
```

### 6. SmartLlmProviderRouter (`src/Honua.Cli.AI/Services/AI/SmartLlmProviderRouter.cs`)

**Purpose**: Routes LLM requests to Anthropic or OpenAI based on task characteristics.

**Routing Strategy**:
- **Critical tasks** → Anthropic (better reasoning)
- **Security/analysis** → Anthropic (detailed analysis)
- **Creative/summarization** → OpenAI (faster, creative)
- **Latency < 2s** → OpenAI (faster responses)
- **Cost < $0.01** → OpenAI (cheaper)

**Usage**:

**Basic routing**:
```csharp
var router = new SmartLlmProviderRouter(factory, logger);

var response = await router.RouteRequestAsync(
    llmRequest,
    new LlmTaskContext
    {
        TaskType = "security-review",
        Criticality = "high",
        MaxLatencyMs = 5000
    },
    cancellationToken
);
// Automatically routes to Anthropic for security review
```

**Second opinion**:
```csharp
// Get first opinion
var firstResponse = await anthropicProvider.CompleteAsync(request, ct);

// Get second opinion from different provider
var secondOpinion = await router.GetSecondOpinionAsync(
    request,
    firstResponse,
    "anthropic",
    new LlmTaskContext { TaskType = "architecture-decision", RequiresSecondOpinion = true },
    ct
);

if (!secondOpinion.Agrees)
{
    Console.WriteLine($"Disagreement: {secondOpinion.Disagreement}");
    Console.WriteLine($"Recommendation: {secondOpinion.Reasoning}");
}

var finalResponse = secondOpinion.RecommendedResponse;
```

**Consensus**:
```csharp
var consensus = await router.GetConsensusAsync(
    request,
    new[] { "anthropic", "openai" },
    new LlmTaskContext { TaskType = "critical-decision", RequiresConsensus = true },
    ct
);

Console.WriteLine($"Agreement: {consensus.AgreementScore:P0}");
Console.WriteLine($"Method: {consensus.ConsensusMethod}");
// Uses majority vote or longest response
```

## Integration with SemanticAgentCoordinator

Update `SemanticAgentCoordinator.cs` to integrate new patterns:

```csharp
public sealed class SemanticAgentCoordinator : IAgentCoordinator
{
    private readonly HierarchicalTaskDecomposer _decomposer;
    private readonly ValidationLoopExecutor _loopExecutor;
    private readonly ArchitectureSwarmCoordinator _swarmCoordinator;
    private readonly SmartLlmProviderRouter _llmRouter;
    private readonly SecurityReviewAgent _securityReviewer;
    private readonly CostReviewAgent _costReviewer;

    // In ProcessRequestAsync:

    // 1. Check if task needs decomposition
    var decompositionDecision = await _decomposer.ShouldDecomposeAsync(
        request, intent, context, cancellationToken);

    if (decompositionDecision.ShouldDecompose)
    {
        var decomposition = await _decomposer.DecomposeAsync(
            request, intent, decompositionDecision.DecompositionStrategy,
            context, cancellationToken);

        // Execute phases hierarchically
        return await ExecuteDecomposedTaskAsync(decomposition, context, cancellationToken);
    }

    // 2. Use swarm for architecture decisions
    if (intent.PrimaryIntent == "architecture")
    {
        var consensus = await _swarmCoordinator.GenerateArchitectureOptionsAsync(
            request, context, cancellationToken);

        // Present options to user, track selection
        // This feeds the learning loop!
        return ConvertSwarmConsensusToResult(consensus);
    }

    // 3. Execute agent with review loop
    var executionResult = await ExecuteSingleAgentAsync(...);

    // 4. Review generated artifacts
    if (executionResult.Success && executionResult.GeneratedArtifact != null)
    {
        var securityReview = await _securityReviewer.ReviewAsync(
            executionResult.ArtifactType,
            executionResult.GeneratedArtifact,
            context,
            cancellationToken);

        var costReview = await _costReviewer.ReviewAsync(
            executionResult.ArtifactType,
            executionResult.GeneratedArtifact,
            context,
            cancellationToken);

        if (!securityReview.Approved || !costReview.Approved)
        {
            // Add warnings to result
            // Or use validation loop to regenerate
        }
    }

    // 5. Use validation loop for execution
    if (intent.PrimaryIntent == "deployment" && !context.DryRun)
    {
        return await _loopExecutor.ExecuteWithValidationAsync(
            ct => ExecuteDeploymentAsync(...),
            (result, ct) => ValidateDeploymentAsync(result, ct),
            (validation, ct) => RemediateDeploymentAsync(validation, ct),
            "Production Deployment",
            cancellationToken
        );
    }
}
```

## Telemetry Enhancements

Enhanced `IPatternUsageTelemetry` with new tracking methods:

```csharp
// Track swarm recommendations and user selections
await telemetry.TrackArchitectureSwarmAsync(
    request: "Deploy for 10k users",
    optionsPresented: new[] { "CostOptimized", "PerformanceOptimized", "Serverless" },
    userSelection: "CostOptimized",
    metadata: new Dictionary<string, object>
    {
        ["estimated_cost"] = 450,
        ["cloud_provider"] = "aws"
    },
    ct
);

// Track review outcomes
await telemetry.TrackReviewOutcomeAsync(
    reviewType: "security",
    patternId: "aws-ecs-production",
    approved: false,
    issuesFound: 3,
    metadata: new Dictionary<string, object>
    {
        ["critical_issues"] = 1,
        ["high_issues"] = 2
    },
    ct
);

// Track decomposition effectiveness
await telemetry.TrackDecompositionAsync(
    strategy: "ParallelByCloudProvider",
    phasesCreated: 3,
    tasksCreated: 9,
    successful: true,
    duration: TimeSpan.FromMinutes(5),
    ct
);

// Track validation loop iterations
await telemetry.TrackValidationLoopAsync(
    action: "AWS ECS Deployment",
    iterationsNeeded: 2,
    ultimatelySucceeded: true,
    failureReasons: new[] { "Health check timeout", "Missing environment variable" },
    ct
);
```

## Learning Loop Implementation

**How user deployments feed back into knowledge base**:

1. **Swarm tracks architecture preferences**:
   ```csharp
   await swarm.TrackUserSelectionAsync(selectedOption, consensus, request, ct);
   // Stores: which architectures presented, which selected, for what requirements
   ```

2. **Review outcomes improve pattern scoring**:
   ```csharp
   await telemetry.TrackReviewOutcomeAsync("security", patternId, approved, issuesFound, metadata, ct);
   // Patterns with frequent security issues get lower confidence scores
   ```

3. **Deployment outcomes update success rates**:
   ```csharp
   await telemetry.TrackDeploymentOutcomeAsync(patternId, success, feedback, metadata, ct);
   // Success/failure rates influence future recommendations
   ```

4. **Validation loops identify problematic patterns**:
   ```csharp
   await telemetry.TrackValidationLoopAsync(action, iterationsNeeded, succeeded, failureReasons, ct);
   // Patterns requiring many retries flagged for improvement
   ```

5. **Pattern search uses aggregated data**:
   ```csharp
   var patterns = await patternStore.SearchPatternsAsync(requirements, ct);
   // Ranking considers:
   // - Historical success rate
   // - User acceptance rate
   // - Security/cost review pass rate
   // - Validation loop success
   ```

## Multi-Provider Strategy

**When to use which provider**:

| Task Type | Primary Provider | Why | Second Opinion? |
|-----------|------------------|-----|-----------------|
| Security Review | Anthropic | Superior reasoning, long context | Yes (OpenAI) for critical |
| Cost Analysis | OpenAI | Fast, structured output | Optional |
| Architecture Design | Swarm (both) | Diverse perspectives | Built-in |
| Code Generation | Anthropic | Better at complex Terraform | No (fast path) |
| Summarization | OpenAI | Faster, cheaper | No |
| Critical Decisions | Anthropic | Best reasoning | Yes (OpenAI) |
| Intent Classification | OpenAI | Fast, consistent | No |
| Troubleshooting | Anthropic | Deep analysis | Optional |

**Cost optimization**:
- Intent classification: OpenAI (fast, cheap)
- Normal agents: Anthropic (quality)
- Second opinions: Only for critical decisions (security, production deployments)
- Swarm consensus: Reserve for architecture decisions (expensive but valuable)

**Latency optimization**:
- Run reviews in parallel: `Task.WhenAll(securityReview, costReview)`
- Swarm agents run in parallel (4 concurrent LLM calls)
- Use OpenAI for latency-sensitive tasks

## Example End-to-End Flow

**User request**: "Deploy production-grade Honua to AWS with security best practices and cost optimization"

1. **Intent Analysis** (OpenAI, fast):
   - Primary intent: `deployment`
   - Required agents: `ArchitectureConsulting`, `DeploymentConfiguration`, `SecurityHardening`
   - Complexity: High

2. **Decomposition** (Anthropic):
   - Decision: Decompose into 3 phases
   - Phase 1: Architecture Design (swarm)
   - Phase 2: Configuration Generation (parallel: Terraform + Security)
   - Phase 3: Review & Execute (sequential)

3. **Phase 1 - Architecture Swarm** (Both providers, parallel):
   - CostOptimizer (OpenAI): "ECS Fargate + Aurora Serverless"
   - PerformanceOptimizer (Anthropic): "ECS EC2 + Aurora Provisioned"
   - SimplicityAdvocate (OpenAI): "Fargate + RDS managed"
   - ScalabilityArchitect (Anthropic): "EKS + Aurora Global"
   - Synthesis (Anthropic): 3 options with tradeoffs
   - **User selects**: "CostOptimizer" → **Tracked for learning**

4. **Phase 2 - Generate Configs** (Anthropic, parallel):
   - DeploymentConfiguration: Generates Terraform
   - SecurityHardening: Generates IAM policies

5. **Phase 3 - Review Loop**:
   - SecurityReviewAgent (Anthropic):
     - Found: Hardcoded password in RDS config (critical)
     - Action: Regenerate with Secrets Manager
   - CostReviewAgent (OpenAI):
     - Found: db.r5.4xlarge oversized ($1200/month savings)
     - Recommendation: Start with db.t3.large
   - **Revision**: Regenerate Terraform with fixes
   - **Second review**: Both pass ✓
   - **Track outcomes for learning**

6. **Phase 4 - Validation Loop** (Anthropic):
   - Iteration 1: Deploy → Health check fails (missing env var)
   - Remediation: Add environment variable
   - Iteration 2: Deploy → Success ✓
   - **Track: 2 iterations needed**

7. **Learning Loop Updates**:
   - Pattern "aws-ecs-fargate-aurora-serverless" used successfully
   - Security issue "hardcoded-rds-password" recorded (prevent in future)
   - Cost optimization "oversized-rds" recorded (suggest t3.large by default)
   - User preferred "CostOptimizer" architecture for this workload

## Multi-Provider Configuration

### Automatic Smart Routing

**Single Provider Mode** (only one API key configured):
- All agents use the same provider
- No routing complexity
- Example: `export ANTHROPIC_API_KEY="sk-ant-..."`

**Multi-Provider Mode** (both keys configured):
- Automatic smart routing enabled by default
- Tasks routed to best provider:
  - **Anthropic**: Security reviews, architecture, complex reasoning
  - **OpenAI**: Intent classification, cost reviews, fast tasks
- Example:
  ```bash
  export ANTHROPIC_API_KEY="sk-ant-..."
  export OPENAI_API_KEY="sk-..."
  ```

**Disable Smart Routing**:
```json
{
  "LlmProvider": {
    "EnableSmartRouting": false
  }
}
```

See [AI_MULTI_PROVIDER_SETUP.md](./AI_MULTI_PROVIDER_SETUP.md) for detailed configuration.

## Benefits

### For Users
- **Better decisions**: Swarm provides multiple perspectives with clear tradeoffs
- **Fewer errors**: Security/cost review catches issues before deployment
- **Higher success rate**: Validation loop automatically fixes common issues
- **Cost savings**: Cost reviewer identifies $500-2000/month in typical savings

### For Honua AI Learning
- **User preferences**: Which architectures users choose for different workloads
- **Common mistakes**: Security/cost issues that appear repeatedly
- **Success patterns**: Which deployment patterns have highest success rates
- **Failure patterns**: Which patterns need more validation iterations

### System Effectiveness
- **Multi-provider strength**: Anthropic for reasoning + OpenAI for speed
- **Parallel execution**: Swarm, reviews, and decomposed tasks run concurrently
- **Automatic improvement**: Every deployment improves future recommendations
- **Graceful degradation**: Falls back if providers fail

## Next Steps

1. Implement telemetry storage backend (PostgreSQL recommended)
2. Add user feedback collection after deployments
3. Build analytics dashboard for pattern performance
4. Implement A/B testing for different swarm strategies
5. Add reinforcement learning for router optimization
