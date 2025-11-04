// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for AI intake conversations.
/// </summary>
public class IntakeMetrics
{
    private readonly Counter<long> _conversationsStarted;
    private readonly Counter<long> _conversationsCompleted;
    private readonly Histogram<double> _conversationDuration;
    private readonly Counter<long> _aiTokensUsed;
    private readonly Counter<double> _aiCostUsd;
    private readonly Counter<long> _conversationErrors;
    private readonly ObservableGauge<int> _activeConversations;

    private int _currentActiveConversations;

    public IntakeMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Intake");

        _conversationsStarted = meter.CreateCounter<long>(
            "conversations_started_total",
            description: "Total number of AI intake conversations started");

        _conversationsCompleted = meter.CreateCounter<long>(
            "conversations_completed_total",
            description: "Total number of AI intake conversations completed");

        _conversationDuration = meter.CreateHistogram<double>(
            "conversation_duration_seconds",
            unit: "s",
            description: "AI conversation duration in seconds");

        _aiTokensUsed = meter.CreateCounter<long>(
            "ai_tokens_used_total",
            description: "Total number of AI tokens consumed");

        _aiCostUsd = meter.CreateCounter<double>(
            "ai_cost_usd_total",
            unit: "USD",
            description: "Total AI cost in USD");

        _conversationErrors = meter.CreateCounter<long>(
            "conversation_errors_total",
            description: "Total number of conversation errors");

        _activeConversations = meter.CreateObservableGauge(
            "active_conversations",
            observeValue: () => _currentActiveConversations,
            description: "Current number of active conversations");
    }

    /// <summary>
    /// Records a conversation start event.
    /// </summary>
    public void RecordConversationStarted(string model)
    {
        _conversationsStarted.Add(1,
            new KeyValuePair<string, object?>("model", model));

        Interlocked.Increment(ref _currentActiveConversations);
    }

    /// <summary>
    /// Records a conversation completion event.
    /// </summary>
    public void RecordConversationCompleted(string model, bool success, TimeSpan duration)
    {
        _conversationsCompleted.Add(1,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("success", success.ToString()));

        _conversationDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("success", success.ToString()));

        Interlocked.Decrement(ref _currentActiveConversations);
    }

    /// <summary>
    /// Records AI token usage.
    /// </summary>
    public void RecordTokenUsage(string model, long promptTokens, long completionTokens)
    {
        var totalTokens = promptTokens + completionTokens;

        _aiTokensUsed.Add(totalTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("token_type", "total"));

        _aiTokensUsed.Add(promptTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("token_type", "prompt"));

        _aiTokensUsed.Add(completionTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("token_type", "completion"));
    }

    /// <summary>
    /// Records AI cost in USD.
    /// </summary>
    public void RecordCost(string model, double costUsd)
    {
        _aiCostUsd.Add(costUsd,
            new KeyValuePair<string, object?>("model", model));
    }

    /// <summary>
    /// Records a conversation error.
    /// </summary>
    public void RecordError(string model, string errorType)
    {
        _conversationErrors.Add(1,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("error_type", errorType));
    }
}
