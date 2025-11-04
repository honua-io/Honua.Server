# Multi-Provider LLM Setup

## Overview

Honua AI supports automatic multi-provider routing when multiple LLM API keys are configured. The system intelligently routes tasks to the best provider based on task characteristics.

## Behavior

### Single Provider Mode
**When only one API key is configured** (e.g., only `OPENAI_API_KEY` or only `ANTHROPIC_API_KEY`):
- All agents use the same provider
- No routing overhead
- Simple, fast execution

### Multi-Provider Mode
**When both Anthropic and OpenAI API keys are configured**:
- Automatic smart routing enabled by default
- Tasks routed based on characteristics:
  - **Anthropic Claude**: Complex reasoning, security reviews, architecture design
  - **OpenAI GPT**: Fast classification, summarization, cost analysis
- Can be disabled with `EnableSmartRouting: false`

## Configuration

### Environment Variables

**Option 1: Anthropic Only**
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
```

**Option 2: OpenAI Only**
```bash
export OPENAI_API_KEY="sk-..."
```

**Option 3: Both (Recommended for Best Results)**
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
export OPENAI_API_KEY="sk-..."
```

### appsettings.json

```json
{
  "LlmProvider": {
    "Provider": "Anthropic",
    "EnableSmartRouting": true,
    "DefaultTemperature": 0.2,
    "DefaultMaxTokens": 4000,
    "OpenAI": {
      "ApiKey": "${ENV:OPENAI_API_KEY}",
      "DefaultModel": "gpt-4o"
    },
    "Anthropic": {
      "ApiKey": "${ENV:ANTHROPIC_API_KEY}",
      "DefaultModel": "claude-3-5-sonnet-20241022"
    }
  }
}
```

**To disable smart routing** (use single provider even if both keys present):
```json
{
  "LlmProvider": {
    "Provider": "Anthropic",
    "EnableSmartRouting": false
  }
}
```

## Routing Strategy

### Automatic Provider Selection

| Task Type | Preferred Provider | Rationale |
|-----------|-------------------|-----------|
| Intent Classification | OpenAI | Fast, consistent, cheaper |
| Security Review | Anthropic | Deep analysis, superior reasoning |
| Cost Review | OpenAI | Structured output, fast |
| Architecture Swarm | Both (parallel) | Diverse perspectives |
| Code Generation | Anthropic | Better at complex Terraform/K8s |
| Troubleshooting | Anthropic | Root cause analysis |
| Summarization | OpenAI | Creative, fast |
| Critical Decisions | Anthropic | Best reasoning |

### Criticality-Based Routing

```csharp
// Critical tasks → Anthropic (if available)
var provider = GetProviderForTask("deployment-execution", criticality: "critical");

// Low criticality → OpenAI (faster, cheaper)
var provider = GetProviderForTask("intent-classification", criticality: "low");
```

## Cost & Performance Impact

### Single Provider
- **Cost**: Depends on chosen provider
  - Anthropic: ~$15/1M tokens (Claude Sonnet)
  - OpenAI: ~$10/1M tokens (GPT-4o)
- **Latency**: Consistent, no routing overhead

### Multi-Provider (Smart Routing)
- **Cost**: Optimized (~20% reduction)
  - Fast tasks → OpenAI (cheaper)
  - Complex tasks → Anthropic (better quality)
- **Latency**: Optimized (~15% reduction)
  - Latency-sensitive → OpenAI (faster)
  - Quality-critical → Anthropic
- **Quality**: Best of both worlds

### Example Cost Breakdown (1000 requests)

**Single Provider (Anthropic)**:
- 1000 requests × 2000 tokens avg × $15/1M = $30

**Single Provider (OpenAI)**:
- 1000 requests × 2000 tokens avg × $10/1M = $20

**Multi-Provider (Smart Routing)**:
- 300 critical requests → Anthropic: 300 × 2000 × $15/1M = $9
- 700 fast requests → OpenAI: 700 × 2000 × $10/1M = $14
- **Total: $23** (23% cheaper than Anthropic-only, with better quality for critical tasks)

## Advanced Features

### Second Opinions

When both providers are available, critical decisions can request second opinions:

```csharp
// Get first opinion
var firstResponse = await anthropicProvider.CompleteAsync(request, ct);

// Get second opinion from OpenAI
var secondOpinion = await router.GetSecondOpinionAsync(
    request,
    firstResponse,
    "anthropic",
    new LlmTaskContext
    {
        TaskType = "security-review",
        Criticality = "critical",
        RequiresSecondOpinion = true
    },
    ct
);

if (!secondOpinion.Agrees)
{
    Console.WriteLine($"⚠️  Providers disagree:");
    Console.WriteLine($"  Anthropic: {firstResponse.Content}");
    Console.WriteLine($"  OpenAI: {secondOpinion.SecondOpinion.Content}");
    Console.WriteLine($"  Recommendation: {secondOpinion.Reasoning}");
}
```

### Consensus Mode

For critical architecture decisions, run both providers in parallel and synthesize:

```csharp
var consensus = await router.GetConsensusAsync(
    request,
    new[] { "anthropic", "openai" },
    new LlmTaskContext
    {
        TaskType = "architecture-decision",
        RequiresConsensus = true
    },
    ct
);

Console.WriteLine($"Agreement: {consensus.AgreementScore:P0}");
Console.WriteLine($"Method: {consensus.ConsensusMethod}");
// Uses majority vote, unanimous agreement, or longest response
```

### Architecture Swarm (Multi-Provider)

The architecture swarm automatically uses mixed providers for diversity:

```csharp
// CostOptimizer → OpenAI (fast, cost-focused)
// PerformanceOptimizer → Anthropic (deep analysis)
// SimplicityAdvocate → OpenAI (creative simplicity)
// ScalabilityArchitect → Anthropic (complex reasoning)

var consensus = await swarm.GenerateArchitectureOptionsAsync(request, context, ct);
// Automatically uses both providers for best results
```

## Fallback Behavior

If one provider fails, the system automatically falls back:

1. **Smart routing fails** → Falls back to default provider
2. **Preferred provider unavailable** → Uses available alternative
3. **No alternative available** → Returns error with clear message

## Best Practices

### Recommended: Both Keys (Multi-Provider)
✅ Best quality (use each provider's strengths)
✅ Best cost (optimize per task)
✅ Best performance (route latency-sensitive tasks)
✅ Resilience (fallback if one provider down)
✅ Second opinions available for critical decisions

### Alternative: Single Provider
✅ Simplicity
✅ Consistent behavior
✅ Lower complexity
⚠️ No optimization
⚠️ No fallback

### When to Disable Smart Routing

Disable `EnableSmartRouting` if:
- You want predictable single-provider behavior
- You're testing provider-specific prompts
- You have API rate limit concerns
- You prefer manual provider selection

## Monitoring

Check which provider was used for each request:

```bash
# Enable debug logging
export LOG_LEVEL=Debug

# View provider routing decisions
honua consultant "deploy to aws" --verbosity debug
# Output: [Debug] Routed intent-classification to openai
# Output: [Debug] Routed deployment-configuration to anthropic
```

## Migration Path

### Phase 1: Start with Single Provider
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
```

### Phase 2: Add Second Provider
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
export OPENAI_API_KEY="sk-..."
# Smart routing automatically enabled
```

### Phase 3: Monitor & Tune
- Check logs for routing decisions
- Verify cost/performance improvements
- Adjust routing strategy if needed

### Phase 4: Advanced Features
- Enable second opinions for critical paths
- Use consensus for architecture decisions
- Track learning loop data

## Troubleshooting

### Both keys configured but routing not working?

Check:
1. `EnableSmartRouting: true` in config
2. Both API keys valid and not expired
3. Debug logging enabled to see routing decisions

### Want to force single provider?

Option 1: Remove one API key
```bash
unset OPENAI_API_KEY
```

Option 2: Disable smart routing
```json
{ "LlmProvider": { "EnableSmartRouting": false } }
```

### Provider selection seems wrong?

File an issue with:
- Task type
- Expected provider
- Actual provider (from debug logs)
- Task context (criticality, latency requirements)

## Future Enhancements

Coming soon:
- [ ] Azure OpenAI support in routing
- [ ] Ollama local model support
- [ ] Cost tracking per provider
- [ ] A/B testing framework
- [ ] Provider performance analytics
- [ ] Custom routing rules
