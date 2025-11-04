// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Observability.Tracing;

/// <summary>
/// Helper service for propagating trace context across service boundaries.
/// Integrates W3C Trace Context with correlation IDs for comprehensive distributed tracing.
/// </summary>
public static class TraceContextPropagation
{
    /// <summary>
    /// Extracts trace context from HTTP request headers and sets it as the current activity's parent.
    /// Supports W3C Trace Context standard (traceparent header).
    /// </summary>
    /// <param name="request">The HTTP request</param>
    /// <returns>The extracted ActivityContext, or default if not found</returns>
    public static ActivityContext ExtractTraceContext(HttpRequest request)
    {
        if (request.Headers.TryGetValue("traceparent", out var traceParent))
        {
            // Parse W3C traceparent header: "00-{trace-id}-{span-id}-{flags}"
            if (ActivityContext.TryParse(traceParent.ToString(), null, out var context))
            {
                return context;
            }
        }

        return default;
    }

    /// <summary>
    /// Injects trace context into HTTP request headers for outgoing requests.
    /// Uses W3C Trace Context standard.
    /// </summary>
    /// <param name="request">The HTTP request message</param>
    /// <param name="activity">The current activity (optional)</param>
    public static void InjectTraceContext(HttpRequestMessage request, Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null)
            return;

        // Add W3C traceparent header
        var traceParent = activity.Id;
        if (!string.IsNullOrEmpty(traceParent))
        {
            request.Headers.Add("traceparent", traceParent);
        }

        // Add W3C tracestate header if present
        var traceState = activity.TraceStateString;
        if (!string.IsNullOrEmpty(traceState))
        {
            request.Headers.Add("tracestate", traceState);
        }

        // Propagate baggage
        foreach (var baggage in activity.Baggage)
        {
            request.Headers.Add($"baggage-{baggage.Key}", baggage.Value);
        }
    }

    /// <summary>
    /// Correlates the current activity with a correlation ID.
    /// Adds both to activity tags and baggage for cross-service propagation.
    /// </summary>
    /// <param name="correlationId">The correlation ID to propagate</param>
    /// <param name="activity">The activity to correlate (optional, uses Current if null)</param>
    public static void CorrelateActivity(string correlationId, Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null || string.IsNullOrEmpty(correlationId))
            return;

        // Add as span attribute (for tracing backends)
        activity.SetTag("correlation.id", correlationId);

        // Add as baggage (for cross-service propagation)
        activity.SetBaggage("correlation.id", correlationId);
    }

    /// <summary>
    /// Gets the correlation ID from the current activity's baggage or tags.
    /// </summary>
    /// <param name="activity">The activity to get correlation ID from (optional)</param>
    /// <returns>The correlation ID, or null if not found</returns>
    public static string? GetCorrelationId(Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null)
            return null;

        // Try baggage first (propagated across services)
        var baggage = activity.GetBaggageItem("correlation.id");
        if (!string.IsNullOrEmpty(baggage))
            return baggage;

        // Fall back to tag (local to this span)
        var tag = activity.GetTagItem("correlation.id");
        return tag?.ToString();
    }

    /// <summary>
    /// Creates a new activity as a child of the specified parent context.
    /// Useful for starting new operations from extracted trace context.
    /// </summary>
    /// <param name="activitySource">The activity source</param>
    /// <param name="activityName">The activity name</param>
    /// <param name="parentContext">The parent activity context</param>
    /// <param name="kind">The activity kind</param>
    /// <param name="tags">Optional initial tags</param>
    /// <returns>The created activity</returns>
    public static Activity? StartActivityFromContext(
        ActivitySource activitySource,
        string activityName,
        ActivityContext parentContext,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        var activity = activitySource.StartActivity(activityName, kind, parentContext);

        if (activity != null && tags != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }

        return activity;
    }

    /// <summary>
    /// Propagates baggage from one activity to another.
    /// Useful when creating child activities or propagating context.
    /// </summary>
    /// <param name="source">The source activity</param>
    /// <param name="target">The target activity</param>
    public static void PropagateBaggage(Activity? source, Activity? target)
    {
        if (source == null || target == null)
            return;

        foreach (var baggage in source.Baggage)
        {
            target.SetBaggage(baggage.Key, baggage.Value);
        }
    }

    /// <summary>
    /// Adds business context to the current activity as baggage.
    /// Baggage is propagated across service boundaries and can be used for filtering/correlation.
    /// </summary>
    /// <param name="key">The baggage key</param>
    /// <param name="value">The baggage value</param>
    /// <param name="activity">The activity to add baggage to (optional)</param>
    public static void AddBaggage(string key, string value, Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            return;

        activity.SetBaggage(key, value);
    }

    /// <summary>
    /// Gets baggage value from the current activity.
    /// </summary>
    /// <param name="key">The baggage key</param>
    /// <param name="activity">The activity to get baggage from (optional)</param>
    /// <returns>The baggage value, or null if not found</returns>
    public static string? GetBaggage(string key, Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null || string.IsNullOrEmpty(key))
            return null;

        return activity.GetBaggageItem(key);
    }

    /// <summary>
    /// Records a trace event with optional attributes.
    /// Events are timestamped annotations on a span.
    /// </summary>
    /// <param name="eventName">The event name</param>
    /// <param name="attributes">Optional event attributes</param>
    /// <param name="activity">The activity to add event to (optional)</param>
    public static void RecordEvent(
        string eventName,
        IEnumerable<KeyValuePair<string, object?>>? attributes = null,
        Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null || string.IsNullOrEmpty(eventName))
            return;

        if (attributes == null)
        {
            activity.AddEvent(new ActivityEvent(eventName));
        }
        else
        {
            var activityTagsCollection = new ActivityTagsCollection();
            foreach (var attr in attributes)
            {
                activityTagsCollection.Add(attr.Key, attr.Value);
            }
            activity.AddEvent(new ActivityEvent(eventName, tags: activityTagsCollection));
        }
    }

    /// <summary>
    /// Links the current activity to another trace context.
    /// Useful for correlating related but independent operations.
    /// </summary>
    /// <param name="linkedContext">The context to link to</param>
    /// <param name="attributes">Optional link attributes</param>
    /// <param name="activity">The activity to add link to (optional)</param>
    public static void AddLink(
        ActivityContext linkedContext,
        IEnumerable<KeyValuePair<string, object?>>? attributes = null,
        Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity == null || linkedContext == default)
            return;

        ActivityTagsCollection? tags = null;
        if (attributes != null)
        {
            tags = new ActivityTagsCollection();
            foreach (var attr in attributes)
            {
                tags.Add(attr.Key, attr.Value);
            }
        }

        activity.AddLink(new ActivityLink(linkedContext, tags));
    }
}
