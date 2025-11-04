# AI Consultant Enhancements - Implementation Roadmap

This document outlines completed enhancements and provides implementation guidance for remaining features.

## ✅ Phase 1: Learning Loop (COMPLETED)

### Pattern Acceptance Tracking
- Tracks when users accept/reject recommended patterns
- Links acceptances to specific patterns via `RecommendedPatternIds`
- Fire-and-forget telemetry to avoid blocking workflow

### Execution Feedback Loop
- Records deployment success/failure for each pattern
- Updates pattern success rates based on real outcomes
- Enables continuous improvement of recommendations

### Pattern Explanations
- LLM-powered "Why this matches" explanations
- Async BuildUserPromptAsync() supports explanation generation
- Temperature 0.3 for consistent, technical explanations

### Pattern Stats CLI
- Command: `honua consultant patterns-stats`
- Individual pattern analytics: acceptance rate, success rate, deployment count
- Top patterns dashboard with color-coded indicators
- Actionable insights for pattern review

## ✅ Phase 2: Advanced Features (COMPLETED)

### Confidence-Based Auto-Approval
- Flag: `--trust-high-confidence`
- Auto-approves when all patterns have High confidence (≥80%)
- Reduces friction for trusted recommendations
- Clear user feedback when auto-approved

### Pattern Versioning
- Version tracking (default: v1)
- Deprecation management (SupersededBy, DeprecatedDate, DeprecationReason)
- Pattern evolution tracking
- Foundation for A/B testing

### Workspace Templates
- 5 built-in templates for common scenarios
- Pre-approved, high-success-rate configurations
- Instant plan generation (no LLM latency)
- Templates: Production PostGIS, STAC Catalog, Vector Tiles, Raster Pipeline, Dev Environment

## 🚧 Phase 3: Agent Intelligence (IN PROGRESS)

### Agent Selection Intelligence

**Goal**: Route requests to the best agent based on confidence scoring and historical performance.

**Implementation Steps**:

1. **Track Agent Performance**
```csharp
// Add to IPatternUsageTelemetry
Task TrackAgentPerformanceAsync(
    string agentName,
    string taskType,
    bool success,
    double confidenceScore,
    string? feedback = null,
    CancellationToken cancellationToken = default);

Task<AgentPerformanceStats> GetAgentStatsAsync(
    string agentName,
    TimeSpan period,
    CancellationToken cancellationToken = default);
```

2. **Agent Confidence Scoring**
```csharp
public sealed class AgentConfidence
{
    public string AgentName { get; init; }
    public double TaskMatchScore { get; init; }  // How well agent matches task type
    public double HistoricalSuccessRate { get; init; }  // Past performance
    public int CompletedTasks { get; init; }
    public double Overall { get; init; }  // Composite score
    public string Level { get; init; }  // High/Medium/Low
}
```

3. **Intelligent Routing in SemanticAgentCoordinator**
```csharp
private async Task<string> SelectBestAgentAsync(
    string userRequest,
    CancellationToken cancellationToken)
{
    var candidates = await ScoreAgentsForRequestAsync(userRequest, cancellationToken);

    // Prefer High confidence agents
    var highConfidence = candidates.Where(c => c.Confidence.Level == "High").ToList();
    if (highConfidence.Any())
    {
        return highConfidence.OrderByDescending(c => c.Confidence.Overall).First().AgentName;
    }

    // Fallback to highest overall score
    return candidates.OrderByDescending(c => c.Confidence.Overall).First().AgentName;
}
```

4. **Learning from Outcomes**
```csharp
// After agent execution
await _telemetry.TrackAgentPerformanceAsync(
    agentName: selectedAgent,
    taskType: ClassifyTaskType(userRequest),
    success: result.Success,
    confidenceScore: agentConfidence.Overall,
    feedback: result.ErrorMessage);
```

## 📝 Phase 4: Conversational Refinement (PENDING)

### Goal: Allow users to refine plans through conversation instead of starting over.

**Implementation Steps**:

1. **Extend ConsultantRequest**
```csharp
public sealed record ConsultantRequest(
    string? Prompt,
    bool DryRun,
    bool AutoApprove,
    bool SuppressLogging,
    string WorkspacePath,
    ConsultantExecutionMode Mode = ConsultantExecutionMode.Auto,
    bool TrustHighConfidence = false,
    ConsultantPlan? PreviousPlan = null,  // NEW
    List<string>? ConversationHistory = null);  // NEW
```

2. **Refinement Command**
```csharp
public sealed class ConsultantRefineCommand : AsyncCommand<ConsultantRefineCommand.Settings>
{
    // honua consultant refine --session <id> --adjustment "make it more secure"
    // honua consultant refine --session <id> --adjustment "optimize for cost"
    // honua consultant refine --session <id> --adjustment "add monitoring"
}
```

3. **Modify SemanticConsultantPlanner**
```csharp
private async Task<string> BuildRefinementPromptAsync(
    ConsultantPlanningContext context,
    ConsultantPlan previousPlan,
    string refinementRequest,
    CancellationToken cancellationToken)
{
    var sb = new StringBuilder();
    sb.AppendLine("### Previous Plan");
    sb.AppendLine(JsonSerializer.Serialize(previousPlan, _jsonOptions));
    sb.AppendLine();
    sb.AppendLine("### Refinement Request");
    sb.AppendLine(refinementRequest);
    sb.AppendLine();
    sb.AppendLine("Generate an improved plan that:");
    sb.AppendLine($"1. Addresses this refinement: {refinementRequest}");
    sb.AppendLine("2. Keeps successful steps from the previous plan");
    sb.AppendLine("3. Adds/modifies steps to fulfill the refinement");
    sb.AppendLine("4. Maintains overall plan coherence");

    return sb.ToString();
}
```

4. **Session Management**
```csharp
public interface IConsultantSessionStore
{
    Task SaveSessionAsync(string sessionId, ConsultantPlan plan, ConsultantPlanningContext context);
    Task<(ConsultantPlan Plan, ConsultantPlanningContext Context)?> GetSessionAsync(string sessionId);
    Task<List<string>> GetRecentSessionsAsync(int count = 10);
}
```

## 💾 Phase 5: Agent History Persistence (PENDING)

### Goal: Store agent interactions in PostgreSQL for long-term learning and context.

**Database Schema**:

```sql
CREATE TABLE agent_interactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id VARCHAR(255) NOT NULL,
    agent_name VARCHAR(255) NOT NULL,
    user_request TEXT NOT NULL,
    agent_response TEXT,
    success BOOLEAN NOT NULL,
    confidence_score DOUBLE PRECISION,
    task_type VARCHAR(100),
    execution_time_ms INTEGER,
    error_message TEXT,
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),

    INDEX idx_session_id (session_id),
    INDEX idx_agent_name (agent_name),
    INDEX idx_created_at (created_at),
    INDEX idx_task_type (task_type)
);

CREATE TABLE agent_session_summaries (
    session_id VARCHAR(255) PRIMARY KEY,
    initial_request TEXT NOT NULL,
    final_outcome VARCHAR(50),  -- 'success' | 'failure' | 'abandoned'
    agents_used VARCHAR(255)[],
    interaction_count INTEGER,
    total_duration_ms INTEGER,
    user_satisfaction SMALLINT,  -- 1-5 rating
    created_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP,
    metadata JSONB
);
```

**Implementation**:

```csharp
public sealed class PostgresAgentHistoryStore : IAgentHistoryStore
{
    public async Task SaveInteractionAsync(
        string sessionId,
        string agentName,
        string userRequest,
        string? agentResponse,
        bool success,
        double? confidenceScore = null,
        string? taskType = null,
        int? executionTimeMs = null,
        string? errorMessage = null,
        JsonDocument? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO agent_interactions
                (session_id, agent_name, user_request, agent_response, success,
                 confidence_score, task_type, execution_time_ms, error_message, metadata)
            VALUES
                (@SessionId, @AgentName, @UserRequest, @AgentResponse, @Success,
                 @ConfidenceScore, @TaskType, @ExecutionTimeMs, @ErrorMessage, @Metadata::jsonb)",
            new
            {
                SessionId = sessionId,
                AgentName = agentName,
                UserRequest = userRequest,
                AgentResponse = agentResponse,
                Success = success,
                ConfidenceScore = confidenceScore,
                TaskType = taskType,
                ExecutionTimeMs = executionTimeMs,
                ErrorMessage = errorMessage,
                Metadata = metadata?.RootElement.GetRawText()
            });
    }

    public async Task<List<AgentInteraction>> GetSessionHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var interactions = await connection.QueryAsync<AgentInteraction>(@"
            SELECT * FROM agent_interactions
            WHERE session_id = @SessionId
            ORDER BY created_at ASC",
            new { SessionId = sessionId });

        return interactions.ToList();
    }

    public async Task<AgentPerformanceStats> GetAgentPerformanceAsync(
        string agentName,
        TimeSpan period,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var cutoff = DateTime.UtcNow - period;

        var stats = await connection.QueryFirstOrDefaultAsync<(int Total, int Successful, double AvgConfidence)>(@"
            SELECT
                COUNT(*)::int as Total,
                SUM(CASE WHEN success THEN 1 ELSE 0 END)::int as Successful,
                AVG(confidence_score) as AvgConfidence
            FROM agent_interactions
            WHERE agent_name = @AgentName
              AND created_at >= @Cutoff",
            new { AgentName = agentName, Cutoff = cutoff });

        return new AgentPerformanceStats
        {
            AgentName = agentName,
            TotalInteractions = stats.Total,
            SuccessfulInteractions = stats.Successful,
            AverageConfidence = stats.AvgConfidence,
            SuccessRate = stats.Total > 0 ? (double)stats.Successful / stats.Total : 0
        };
    }
}
```

## 📊 Performance Metrics

Track these KPIs to measure consultant effectiveness:

### Pattern Metrics:
- **Acceptance Rate**: % of recommended patterns users accept
- **Success Rate**: % of deployments that succeed
- **Time to Value**: Average time from recommendation to successful deployment
- **Pattern Churn**: How often patterns are deprecated/updated

### Agent Metrics:
- **Task Match Accuracy**: How well agent selection matches task type
- **Average Confidence**: Mean confidence score across interactions
- **User Satisfaction**: Explicit ratings + implicit (retry rate)
- **Specialization Score**: Performance variance across task types

### Consultant Metrics:
- **Auto-Approval Rate**: % of plans auto-approved via high confidence
- **Refinement Count**: Average refinements per session
- **Template Usage**: % of plans using templates vs. custom
- **Overall Success Rate**: End-to-end deployment success

## 🔮 Future Enhancements

### 1. A/B Testing Framework
- Run parallel pattern variations
- Track comparative performance
- Automatically promote winners

### 2. Collaborative Filtering
- "Users who deployed X also deployed Y"
- Pattern recommendation based on similar workspaces
- Community-driven pattern curation

### 3. Cost Optimization
- Track actual vs. estimated deployment costs
- Recommend cost-saving alternatives
- Budget-aware pattern filtering

### 4. Security Scoring
- CVE scanning for pattern components
- Compliance validation (SOC2, HIPAA, etc.)
- Automatic security patching recommendations

### 5. Multi-Workspace Orchestration
- Coordinate deployments across workspaces
- Dependency management between environments
- Blue-green deployment patterns

## 📚 References

- **Pattern Confidence Algorithm**: See `PatternConfidence.cs`
- **Telemetry Schema**: See `PostgresPatternUsageTelemetry.cs`
- **Template Definitions**: See `PatternTemplates.cs`
- **Agent Coordination**: See `SemanticAgentCoordinator.cs`

---

**Status**: 7/10 features complete, 3 remaining
**Next Priority**: Agent Selection Intelligence → Conversational Refinement → Agent History Persistence
