// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Buffers;
using System.Diagnostics;

namespace Honua.Server.Observability.Tracing;

/// <summary>
/// Extension methods for creating custom spans with OpenTelemetry.
/// </summary>
public static class ActivityExtensions
{
    private static readonly ActivitySource BuildActivitySource = new("Honua.BuildQueue");
    private static readonly ActivitySource CacheActivitySource = new("Honua.Cache");
    private static readonly ActivitySource RegistryActivitySource = new("Honua.Registry");
    private static readonly ActivitySource IntakeActivitySource = new("Honua.Intake");
    private static readonly SearchValues<char> CacheKeyDelimiters = SearchValues.Create([':', '/', '_']);

    /// <summary>
    /// Creates a new activity (span) for build operations.
    /// </summary>
    public static Activity? StartBuildActivity(
        string operationName,
        string? buildId = null,
        string? tier = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = BuildActivitySource.StartActivity(operationName, kind);

        if (activity != null)
        {
            if (buildId != null)
                activity.SetTag("build.id", buildId);

            if (tier != null)
                activity.SetTag("build.tier", tier);
        }

        return activity;
    }

    /// <summary>
    /// Creates a new activity (span) for cache operations.
    /// </summary>
    public static Activity? StartCacheActivity(
        string operationName,
        string? cacheKey = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = CacheActivitySource.StartActivity(operationName, kind);

        if (activity != null && cacheKey != null)
        {
            // Don't include the full key to avoid high cardinality
            activity.SetTag("cache.key_prefix", GetKeyPrefix(cacheKey));
        }

        return activity;
    }

    /// <summary>
    /// Creates a new activity (span) for registry operations.
    /// </summary>
    public static Activity? StartRegistryActivity(
        string operationName,
        string? provider = null,
        string? registryId = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = RegistryActivitySource.StartActivity(operationName, kind);

        if (activity != null)
        {
            if (provider != null)
                activity.SetTag("registry.provider", provider);

            if (registryId != null)
                activity.SetTag("registry.id", registryId);
        }

        return activity;
    }

    /// <summary>
    /// Creates a new activity (span) for AI intake operations.
    /// </summary>
    public static Activity? StartIntakeActivity(
        string operationName,
        string? conversationId = null,
        string? model = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = IntakeActivitySource.StartActivity(operationName, kind);

        if (activity != null)
        {
            if (conversationId != null)
                activity.SetTag("conversation.id", conversationId);

            if (model != null)
                activity.SetTag("ai.model", model);
        }

        return activity;
    }

    /// <summary>
    /// Records an exception on the current activity.
    /// </summary>
    public static Activity? RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null)
            return null;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);

        return activity;
    }

    /// <summary>
    /// Marks the activity as successful.
    /// </summary>
    public static Activity? SetSuccess(this Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        return activity;
    }

    /// <summary>
    /// Adds a custom tag to the activity.
    /// </summary>
    public static Activity? AddTag(this Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value);
        return activity;
    }

    /// <summary>
    /// Adds multiple custom tags to the activity.
    /// </summary>
    public static Activity? AddTags(this Activity? activity, params (string Key, object? Value)[] tags)
    {
        if (activity == null)
            return null;

        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }

        return activity;
    }

    /// <summary>
    /// Adds an event to the activity.
    /// </summary>
    public static Activity? AddEvent(this Activity? activity, string eventName, params (string Key, object? Value)[] tags)
    {
        if (activity == null)
            return null;

        var activityTags = new ActivityTagsCollection(
            tags.Select(t => new KeyValuePair<string, object?>(t.Key, t.Value))
        );

        activity.AddEvent(new ActivityEvent(eventName, tags: activityTags));

        return activity;
    }

    private static string GetKeyPrefix(string cacheKey)
    {
        // Extract just the prefix before any delimiters
        var delimiterIndex = cacheKey.AsSpan().IndexOfAny(CacheKeyDelimiters);
        return delimiterIndex > 0 ? cacheKey.Substring(0, delimiterIndex) : cacheKey;
    }
}

/// <summary>
/// Example usage class demonstrating how to use activity extensions.
/// </summary>
public class TracingExamples
{
    public async Task BuildExampleAsync(string buildId, string tier)
    {
        using var activity = ActivityExtensions.StartBuildActivity("ProcessBuild", buildId, tier);

        try
        {
            activity?.AddEvent("BuildStarted", ("tier", tier));

            // Perform build operations
            await Task.Delay(100);

            activity?.AddEvent("BuildCompleted", ("duration_ms", 100));
            activity?.SetSuccess();
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
    }

    public async Task<string?> CacheExampleAsync(string key)
    {
        using var activity = ActivityExtensions.StartCacheActivity("CacheLookup", key);

        try
        {
            // Simulate cache lookup
            await Task.Delay(10);

            var hit = Random.Shared.Next(2) == 0;
            activity?.AddTag("cache.hit", hit);

            if (hit)
            {
                activity?.AddEvent("CacheHit");
                activity?.SetSuccess();
                return "cached-value";
            }

            activity?.AddEvent("CacheMiss");
            activity?.SetSuccess();
            return null;
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
    }
}
