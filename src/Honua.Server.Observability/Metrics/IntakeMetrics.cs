// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for AI intake conversations.
/// </summary>
public class IntakeMetrics
{
    private readonly Counter<long> conversationsStarted;
    private readonly Counter<long> conversationsCompleted;
    private readonly Histogram<double> conversationDuration;
    private readonly Counter<long> aiTokensUsed;
    private readonly Counter<double> aiCostUsd;
    private readonly Counter<long> conversationErrors;
    private readonly ObservableGauge<int> activeConversations;

    private int currentActiveConversations;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntakeMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    public IntakeMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Intake");

        this.conversationsStarted = meter.CreateCounter<long>(
            "conversations_started_total",
            description: "Total number of AI intake conversations started");

        this.conversationsCompleted = meter.CreateCounter<long>(
            "conversations_completed_total",
            description: "Total number of AI intake conversations completed");

        this.conversationDuration = meter.CreateHistogram<double>(
            "conversation_duration_seconds",
            unit: "s",
            description: "AI conversation duration in seconds");

        this.aiTokensUsed = meter.CreateCounter<long>(
            "ai_tokens_used_total",
            description: "Total number of AI tokens consumed");

        this.aiCostUsd = meter.CreateCounter<double>(
            "ai_cost_usd_total",
            unit: "USD",
            description: "Total AI cost in USD");

        this.conversationErrors = meter.CreateCounter<long>(
            "conversation_errors_total",
            description: "Total number of conversation errors");

        this.activeConversations = meter.CreateObservableGauge(
            "active_conversations",
            observeValue: () => this.currentActiveConversations,
            description: "Current number of active conversations");
    }

    /// <summary>
    /// Records a conversation start event.
    /// </summary>
    /// <param name="model">The AI model.</param>
    public void RecordConversationStarted(string model)
    {
        this.conversationsStarted.Add(1,
            new KeyValuePair<string, object?>("model", model));

        Interlocked.Increment(ref this.currentActiveConversations);
    }

    /// <summary>
    /// Records a conversation completion event.
    /// </summary>
    /// <param name="model">The AI model.</param>
    /// <param name="success">Whether the conversation succeeded.</param>
    /// <param name="duration">The conversation duration.</param>
    public void RecordConversationCompleted(string model, bool success, TimeSpan duration)
    {
        this.conversationsCompleted.Add(1,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("success", success.ToString()));

        this.conversationDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("success", success.ToString()));

        Interlocked.Decrement(ref this.currentActiveConversations);
    }

    /// <summary>
    /// Records AI token usage.
    /// </summary>
    /// <param name="model">The AI model.</param>
    /// <param name="promptTokens">The prompt tokens.</param>
    /// <param name="completionTokens">The completion tokens.</param>
    public void RecordTokenUsage(string model, long promptTokens, long completionTokens)
    {
        var totalTokens = promptTokens + completionTokens;

        this.aiTokensUsed.Add(totalTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("token_type", "total"));

        this.aiTokensUsed.Add(promptTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("token_type", "prompt"));

        this.aiTokensUsed.Add(completionTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("token_type", "completion"));
    }

    /// <summary>
    /// Records AI cost in USD.
    /// </summary>
    /// <param name="model">The AI model.</param>
    /// <param name="costUsd">The cost in USD.</param>
    public void RecordCost(string model, double costUsd)
    {
        this.aiCostUsd.Add(costUsd,
            new KeyValuePair<string, object?>("model", model));
    }

    /// <summary>
    /// Records a conversation error.
    /// </summary>
    /// <param name="model">The AI model.</param>
    /// <param name="errorType">The error type.</param>
    public void RecordError(string model, string errorType)
    {
        this.conversationErrors.Add(1,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("error_type", errorType));
    }
}
