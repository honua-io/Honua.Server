# Honua AI Learning Dashboard

## Overview

The Learning Dashboard provides comprehensive analytics and insights into the Honua AI feedback loop, showing how patterns, agents, and recommendations improve over time based on user interactions.

## Features

### 1. Enhanced Feedback Tracking

The system now tracks richer interaction data beyond simple accept/reject:

#### Configuration Modifications
- **What changed**: Tracks specific fields users modify in recommended configurations
- **Frequency analysis**: Identifies which parameters users most often adjust
- **Pattern improvement**: Helps identify patterns that need better default configurations

#### Interaction Timing
- **Time to decision**: Measures how long users take to accept/reject recommendations
- **Quick decisions**: High confidence patterns should have faster decision times
- **Hesitation signals**: Long decision times may indicate unclear recommendations

#### Follow-up Questions
- **Question counting**: Tracks how many clarifying questions users ask per pattern
- **Documentation gaps**: High question counts indicate missing or unclear documentation
- **Learning opportunity**: Helps improve pattern explanations

#### User Satisfaction
- **Rating system**: Optional 1-5 star rating for pattern recommendations
- **Feedback text**: Free-form user feedback for qualitative insights
- **Trend tracking**: Monitor satisfaction improvements over time

### 2. Learning Dashboard Command

Access the dashboard via CLI:

```bash
# View full dashboard
honua ai dashboard

# View last 24 weeks of data
honua ai dashboard --weeks 24

# Filter by specific pattern
honua ai dashboard --pattern "aws-standard-gis-stack"

# Filter by specific agent
honua ai dashboard --agent "DeploymentConfigurationAgent"

# Show only actionable insights
honua ai dashboard --insights-only

# Custom database connection
honua ai dashboard --connection-string "Host=localhost;Database=honua;..."
```

### 3. Dashboard Sections

#### Overall Learning Statistics
- Total patterns and interactions
- Average acceptance rate
- Average confidence scores
- Average decision times
- Agent success rates
- Deployment success rates
- Cost accuracy metrics

#### Pattern Learning Metrics
- Top performing patterns
- Acceptance rates by pattern
- Modification rates (how often users change configs)
- Average confidence scores
- Average decision times
- Follow-up questions per pattern
- User satisfaction ratings

#### Agent Performance Trends
- Agent success rates over time
- Execution counts
- Average response times
- Performance improvements week-over-week

#### Feature Importance Analysis
- Most frequently modified configuration fields
- Identifies which parameters users adjust most
- Helps prioritize pattern configuration improvements
- Pattern-specific field analysis

#### User Satisfaction Trends
- Weekly satisfaction ratings
- Positive vs negative feedback counts
- Satisfaction trends over time
- Response rates

#### Actionable Insights
Automatically identifies patterns that need attention:

- **Low acceptance** (<30%): Pattern may not match user needs
- **High modification rate** (>70%): Recommended config needs improvement
- **Long decision time** (>5 min): Pattern explanation may be unclear
- **Many follow-up questions** (>3): Documentation needs improvement
- **Low satisfaction** (<3.0): Pattern needs review
- **Performing well**: Good pattern match (>80% acceptance, <20% modification)

## Database Schema

### New Tables

#### `pattern_interaction_feedback`
Stores detailed interaction data for each pattern recommendation:

```sql
CREATE TABLE pattern_interaction_feedback (
    id UUID PRIMARY KEY,
    pattern_id VARCHAR(200),
    deployment_id VARCHAR(100),

    -- Recommendation context
    recommended_at TIMESTAMPTZ,
    recommendation_rank INTEGER,
    confidence_score DECIMAL(5,4),

    -- User interaction timing
    decision_timestamp TIMESTAMPTZ,
    time_to_decision_seconds INTEGER,

    -- User decision
    was_accepted BOOLEAN,
    was_modified BOOLEAN,

    -- Configuration changes
    recommended_config JSONB,
    actual_config JSONB,
    config_modifications JSONB,

    -- Interaction signals
    follow_up_questions_count INTEGER,
    user_hesitation_indicators JSONB,

    -- Explicit feedback
    user_satisfaction_rating INTEGER,
    user_feedback_text TEXT
);
```

### Analytics Views

#### `pattern_learning_metrics`
Aggregated metrics per pattern (last 90 days)

#### `pattern_confidence_trends`
Weekly confidence and acceptance trends

#### `feature_importance_analysis`
Most frequently modified configuration fields

## API Usage

### Tracking Pattern Interactions

```csharp
// Initial recommendation
var feedback = new PatternInteractionFeedback
{
    PatternId = "aws-standard-gis-stack",
    RecommendationRank = 1,
    ConfidenceScore = 0.85m,
    RecommendedConfigJson = JsonSerializer.Serialize(config)
};
await telemetryService.TrackPatternInteractionAsync(feedback);

// User makes decision
await telemetryService.UpdatePatternDecisionAsync(
    feedback.Id,
    wasAccepted: true,
    decisionTimestamp: DateTime.UtcNow,
    actualConfigJson: JsonSerializer.Serialize(modifiedConfig),
    configModificationsJson: JsonSerializer.Serialize(diff)
);

// Track follow-up questions
await telemetryService.IncrementFollowUpQuestionsAsync(feedback.Id);

// Record satisfaction
await telemetryService.RecordUserSatisfactionAsync(
    feedback.Id,
    rating: 5,
    feedbackText: "Perfect match for our needs!"
);
```

### Querying Dashboard Metrics

```csharp
var dashboardService = new LearningDashboardService(connectionString, logger);

// Get overall summary
var summary = await dashboardService.GetLearningStatsSummaryAsync(
    timeWindow: TimeSpan.FromDays(30)
);

// Get pattern confidence trends
var trends = await dashboardService.GetPatternConfidenceTrendsAsync(
    patternId: "aws-standard-gis-stack",
    weeksBack: 12
);

// Get feature importance
var features = await dashboardService.GetFeatureImportanceAsync(
    patternId: "aws-standard-gis-stack"
);

// Get actionable insights
var insights = await dashboardService.GetPatternInsightsAsync();
```

## Optimization Recommendations

### 1. Online Learning
The system now supports immediate feedback signals (acceptance/rejection) while still performing deeper analysis offline (deployment outcomes).

### 2. Cold Start Handling
For new patterns with no historical data:
- Use transfer learning from similar patterns
- Apply expert rules as priors
- Mark recommendations as "exploratory" (untested but theoretically sound)

### 3. Richer Feedback Signals
Track:
- **User edits**: What they changed indicates what was wrong
- **Partial adoption**: Which parts of the pattern they used
- **Time-to-decision**: Quick acceptance = high confidence match
- **Questions**: Confusion indicates poor explanation

### 4. Learning Loop Observability
Monitor:
- Pattern confidence trends over time
- Agent accuracy improvements
- Feature importance (which factors matter most)
- A/B test results for routing decisions

## Environment Variables

```bash
# Set database connection string
export HONUA_TELEMETRY_CONNECTION_STRING="Host=localhost;Database=honua;Username=postgres;Password=yourpassword"
```

## Best Practices

1. **Regular Review**: Check the dashboard weekly to identify patterns needing attention
2. **Act on Insights**: Address patterns with low acceptance or high modification rates
3. **Monitor Trends**: Look for improvements over time as patterns learn
4. **User Feedback**: Encourage users to provide satisfaction ratings
5. **Feature Analysis**: Use feature importance to prioritize configuration improvements

## Future Enhancements

- **Automated pattern tuning**: Automatically adjust pattern configs based on modification patterns
- **A/B testing**: Test pattern variations to optimize recommendations
- **Predictive analytics**: Predict which patterns will work best before recommending
- **Active learning**: Request specific feedback when confidence is low
- **Transfer learning**: Learn from similar patterns to improve cold start performance
