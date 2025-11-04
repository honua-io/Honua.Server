// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Provides helper utilities for creating and managing OpenTelemetry Activity scopes.
/// Consolidates common patterns for distributed tracing, automatic status recording,
/// and tag management across the Honua codebase.
/// </summary>
/// <remarks>
/// <para>
/// This helper eliminates ~600 lines of duplicated Activity creation and management code
/// by providing:
/// </para>
/// <list type="bullet">
/// <item><description>Automatic activity lifecycle management (create, dispose)</description></item>
/// <item><description>Automatic success/error status recording</description></item>
/// <item><description>Null-safe operations (handles disabled tracing gracefully)</description></item>
/// <item><description>Fluent API for tag management</description></item>
/// <item><description>Builder pattern for complex scenarios</description></item>
/// </list>
/// <para>
/// <b>Example Usage (Simple):</b>
/// <code>
/// // Before (10+ lines):
/// using var activity = HonuaTelemetry.Stac.StartActivity("STAC PostCollection");
/// activity?.SetTag("stac.operation", "PostCollection");
/// try
/// {
///     var result = await DoWorkAsync();
///     activity?.SetTag("stac.collection_id", result.Id);
///     return result;
/// }
/// catch (Exception ex)
/// {
///     activity?.SetTag("error", true);
///     activity?.SetTag("error.message", ex.Message);
///     activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
///     throw;
/// }
///
/// // After (2 lines):
/// return await ActivityScope.ExecuteAsync(
///     HonuaTelemetry.Stac,
///     "STAC PostCollection",
///     [("stac.operation", "PostCollection")],
///     async activity =>
///     {
///         var result = await DoWorkAsync();
///         activity.AddTag("stac.collection_id", result.Id);
///         return result;
///     });
/// </code>
/// </para>
/// <para>
/// <b>Example Usage (Builder):</b>
/// <code>
/// var result = await ActivityScope.Create(HonuaTelemetry.Database, "DatabaseQuery")
///     .WithTag("db.system", "postgresql")
///     .WithTag("db.name", databaseName)
///     .WithTag("db.operation", "SELECT")
///     .WithKind(ActivityKind.Client)
///     .ExecuteAsync(async activity =>
///     {
///         var results = await ExecuteQueryAsync();
///         activity.AddTag("db.rows_returned", results.Count);
///         return results;
///     });
/// </code>
/// </para>
/// </remarks>
public static class ActivityScope
{
    /// <summary>
    /// Executes an operation within an Activity scope with automatic lifecycle management.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// Automatically sets the activity status to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Ok - when operation completes successfully</description></item>
    /// <item><description>Error - when operation throws an exception (sets error tags and re-throws)</description></item>
    /// </list>
    /// <para>
    /// The activity parameter passed to the operation may be null if tracing is disabled
    /// or sampling has filtered this activity. Always use the null-safe extension methods
    /// like AddTag() or check for null before accessing activity properties.
    /// </para>
    /// </remarks>
    public static async Task<T> ExecuteAsync<T>(
        ActivitySource activitySource,
        string activityName,
        Func<Activity?, Task<T>> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);

        try
        {
            var result = await operation(activity).ConfigureAwait(false);
            activity.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation within an Activity scope with initial tags and automatic lifecycle management.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="tags">Initial tags to set on the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, tags, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This overload allows you to specify initial tags that will be set on the activity
    /// before the operation begins. This is useful for context that is known at the start
    /// of the operation (e.g., operation type, entity IDs, etc.).
    /// </para>
    /// <para>
    /// Automatically sets the activity status to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Ok - when operation completes successfully</description></item>
    /// <item><description>Error - when operation throws an exception (sets error tags and re-throws)</description></item>
    /// </list>
    /// </remarks>
    public static async Task<T> ExecuteAsync<T>(
        ActivitySource activitySource,
        string activityName,
        IEnumerable<(string Key, object? Value)> tags,
        Func<Activity?, Task<T>> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (tags == null)
            throw new ArgumentNullException(nameof(tags));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);
        activity.AddTags(tags.ToArray());

        try
        {
            var result = await operation(activity).ConfigureAwait(false);
            activity.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a void operation within an Activity scope with automatic lifecycle management.
    /// </summary>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for operations that don't return a value.
    /// </para>
    /// <para>
    /// Automatically sets the activity status to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Ok - when operation completes successfully</description></item>
    /// <item><description>Error - when operation throws an exception (sets error tags and re-throws)</description></item>
    /// </list>
    /// </remarks>
    public static async Task ExecuteAsync(
        ActivitySource activitySource,
        string activityName,
        Func<Activity?, Task> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);

        try
        {
            await operation(activity).ConfigureAwait(false);
            activity.RecordSuccess();
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a void operation within an Activity scope with initial tags and automatic lifecycle management.
    /// </summary>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="tags">Initial tags to set on the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, tags, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for operations that don't return a value and need initial tags.
    /// </para>
    /// <para>
    /// Automatically sets the activity status to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Ok - when operation completes successfully</description></item>
    /// <item><description>Error - when operation throws an exception (sets error tags and re-throws)</description></item>
    /// </list>
    /// </remarks>
    public static async Task ExecuteAsync(
        ActivitySource activitySource,
        string activityName,
        IEnumerable<(string Key, object? Value)> tags,
        Func<Activity?, Task> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (tags == null)
            throw new ArgumentNullException(nameof(tags));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);
        activity.AddTags(tags.ToArray());

        try
        {
            await operation(activity).ConfigureAwait(false);
            activity.RecordSuccess();
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous operation within an Activity scope with automatic lifecycle management.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous overload useful for test scenarios and synchronous operations.
    /// For async operations, prefer the ExecuteAsync overloads.
    /// </para>
    /// <para>
    /// Automatically sets the activity status to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Ok - when operation completes successfully</description></item>
    /// <item><description>Error - when operation throws an exception (sets error tags and re-throws)</description></item>
    /// </list>
    /// </remarks>
    public static T Execute<T>(
        ActivitySource activitySource,
        string activityName,
        Func<Activity?, T> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);

        try
        {
            var result = operation(activity);
            activity.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous operation within an Activity scope with initial tags and automatic lifecycle management.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="tags">Initial tags to set on the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, tags, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous overload useful for test scenarios and synchronous operations.
    /// For async operations, prefer the ExecuteAsync overloads.
    /// </para>
    /// <para>
    /// This overload allows you to specify initial tags that will be set on the activity
    /// before the operation begins. This is useful for context that is known at the start
    /// of the operation (e.g., operation type, entity IDs, etc.).
    /// </para>
    /// </remarks>
    public static T Execute<T>(
        ActivitySource activitySource,
        string activityName,
        IEnumerable<(string Key, object? Value)> tags,
        Func<Activity?, T> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (tags == null)
            throw new ArgumentNullException(nameof(tags));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);
        activity.AddTags(tags.ToArray());

        try
        {
            var result = operation(activity);
            activity.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous void operation within an Activity scope with automatic lifecycle management.
    /// </summary>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous overload useful for test scenarios and synchronous operations.
    /// For async operations, prefer the ExecuteAsync overloads.
    /// </para>
    /// <para>
    /// This is a convenience overload for operations that don't return a value.
    /// </para>
    /// </remarks>
    public static void Execute(
        ActivitySource activitySource,
        string activityName,
        Action<Activity?> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);

        try
        {
            operation(activity);
            activity.RecordSuccess();
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous void operation within an Activity scope with initial tags and automatic lifecycle management.
    /// </summary>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="tags">Initial tags to set on the activity.</param>
    /// <param name="operation">The operation to execute within the activity scope.</param>
    /// <param name="kind">The ActivityKind (default: Internal).</param>
    /// <exception cref="ArgumentNullException">Thrown when activitySource, activityName, tags, or operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous overload useful for test scenarios and synchronous operations.
    /// For async operations, prefer the ExecuteAsync overloads.
    /// </para>
    /// <para>
    /// This is a convenience overload for operations that don't return a value and need initial tags.
    /// </para>
    /// </remarks>
    public static void Execute(
        ActivitySource activitySource,
        string activityName,
        IEnumerable<(string Key, object? Value)> tags,
        Action<Activity?> operation,
        ActivityKind kind = ActivityKind.Internal)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));
        if (tags == null)
            throw new ArgumentNullException(nameof(tags));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = activitySource.StartActivity(activityName, kind);
        activity.AddTags(tags.ToArray());

        try
        {
            operation(activity);
            activity.RecordSuccess();
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Creates a builder for constructing an Activity scope with fluent API.
    /// </summary>
    /// <param name="activitySource">The ActivitySource to create the activity from.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <returns>An ActivityScopeBuilder instance for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when activitySource or activityName is null.</exception>
    /// <remarks>
    /// <para>
    /// The builder pattern is useful when you need to conditionally add tags or
    /// configure the activity before execution.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var builder = ActivityScope.Create(HonuaTelemetry.Database, "Query")
    ///     .WithTag("db.system", "postgresql");
    ///
    /// if (userId != null)
    ///     builder.WithTag("user.id", userId);
    ///
    /// var result = await builder.ExecuteAsync(async activity => {
    ///     return await DoWorkAsync();
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static ActivityScopeBuilder Create(ActivitySource activitySource, string activityName)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentNullException(nameof(activityName));

        return new ActivityScopeBuilder(activitySource, activityName);
    }

    /// <summary>
    /// Adds a single tag to the activity in a null-safe manner.
    /// </summary>
    /// <param name="activity">The activity to add the tag to (may be null).</param>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The same activity instance for fluent chaining, or null if activity is null.</returns>
    /// <remarks>
    /// <para>
    /// This extension method safely handles null activities, which occur when:
    /// </para>
    /// <list type="bullet">
    /// <item><description>OpenTelemetry tracing is disabled</description></item>
    /// <item><description>The activity was not sampled (filtered by sampling policy)</description></item>
    /// <item><description>No listeners are attached to the ActivitySource</description></item>
    /// </list>
    /// <para>
    /// Always prefer this over direct SetTag() calls to avoid null reference exceptions.
    /// </para>
    /// </remarks>
    public static Activity? AddTag(this Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value);
        return activity;
    }

    /// <summary>
    /// Adds multiple tags to the activity in a null-safe manner.
    /// </summary>
    /// <param name="activity">The activity to add tags to (may be null).</param>
    /// <param name="tags">Array of key-value pairs to add as tags.</param>
    /// <returns>The same activity instance for fluent chaining, or null if activity is null.</returns>
    /// <remarks>
    /// <para>
    /// This extension method safely handles null activities and allows you to add
    /// multiple tags efficiently without repeated null checks.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// activity.AddTags(
    ///     ("operation", "query"),
    ///     ("table", tableName),
    ///     ("rows", rowCount)
    /// );
    /// </code>
    /// </para>
    /// </remarks>
    public static Activity? AddTags(this Activity? activity, params (string Key, object? Value)[] tags)
    {
        if (activity == null || tags == null)
            return activity;

        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }

        return activity;
    }

    /// <summary>
    /// Records a successful operation by setting the activity status to Ok.
    /// </summary>
    /// <param name="activity">The activity to record success on (may be null).</param>
    /// <returns>The same activity instance for fluent chaining, or null if activity is null.</returns>
    /// <remarks>
    /// <para>
    /// This method sets the ActivityStatusCode to Ok, indicating the operation
    /// completed successfully. This is optional in most cases as Ok is the default,
    /// but it can be useful for explicit documentation and to override a previous
    /// status setting.
    /// </para>
    /// </remarks>
    public static Activity? RecordSuccess(this Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        return activity;
    }

    /// <summary>
    /// Records an error on the activity by setting error tags and status.
    /// </summary>
    /// <param name="activity">The activity to record the error on (may be null).</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <returns>The same activity instance for fluent chaining, or null if activity is null.</returns>
    /// <remarks>
    /// <para>
    /// This method follows OpenTelemetry semantic conventions by setting:
    /// </para>
    /// <list type="bullet">
    /// <item><description>error = true</description></item>
    /// <item><description>error.type = Exception type name</description></item>
    /// <item><description>error.message = Exception message</description></item>
    /// <item><description>ActivityStatusCode = Error with exception message</description></item>
    /// </list>
    /// <para>
    /// If the exception has a stack trace, it will also be added to the activity.
    /// </para>
    /// </remarks>
    public static Activity? RecordError(this Activity? activity, Exception ex)
    {
        if (activity == null || ex == null)
            return activity;

        activity.SetTag("error", true);
        activity.SetTag("error.type", ex.GetType().Name);
        activity.SetTag("error.message", ex.Message);

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            activity.SetTag("error.stack_trace", ex.StackTrace);
        }

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        return activity;
    }

    /// <summary>
    /// Records a custom status on the activity.
    /// </summary>
    /// <param name="activity">The activity to record the status on (may be null).</param>
    /// <param name="statusCode">The status code to set.</param>
    /// <param name="description">Optional description of the status.</param>
    /// <returns>The same activity instance for fluent chaining, or null if activity is null.</returns>
    /// <remarks>
    /// <para>
    /// This method allows you to set custom status codes. The OpenTelemetry specification
    /// defines three status codes:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Unset (default) - Status has not been set</description></item>
    /// <item><description>Ok - Operation completed successfully</description></item>
    /// <item><description>Error - Operation failed</description></item>
    /// </list>
    /// </remarks>
    public static Activity? RecordStatus(this Activity? activity, ActivityStatusCode statusCode, string? description = null)
    {
        activity?.SetStatus(statusCode, description);
        return activity;
    }

    /// <summary>
    /// Adds an event to the activity with optional tags.
    /// </summary>
    /// <param name="activity">The activity to add the event to (may be null).</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="tags">Optional tags to attach to the event.</param>
    /// <returns>The same activity instance for fluent chaining, or null if activity is null.</returns>
    /// <remarks>
    /// <para>
    /// Events allow you to record significant points in time during an operation's
    /// execution. Unlike tags which are key-value pairs attached to the entire span,
    /// events are timestamped annotations.
    /// </para>
    /// <para>
    /// Example use cases:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Cache hit/miss events</description></item>
    /// <item><description>Retry attempts</description></item>
    /// <item><description>Circuit breaker state changes</description></item>
    /// <item><description>Significant milestones in long operations</description></item>
    /// </list>
    /// </remarks>
    public static Activity? AddEvent(this Activity? activity, string eventName, params (string Key, object? Value)[] tags)
    {
        if (activity == null || string.IsNullOrWhiteSpace(eventName))
            return activity;

        if (tags == null || tags.Length == 0)
        {
            activity.AddEvent(new ActivityEvent(eventName));
        }
        else
        {
            var tagList = new List<KeyValuePair<string, object?>>();
            foreach (var (key, value) in tags)
            {
                tagList.Add(new KeyValuePair<string, object?>(key, value));
            }

            activity.AddEvent(new ActivityEvent(eventName, tags: new ActivityTagsCollection(tagList)));
        }

        return activity;
    }
}

/// <summary>
/// Builder for creating Activity scopes with fluent configuration.
/// </summary>
/// <remarks>
/// <para>
/// This builder allows you to construct an Activity scope with a fluent API,
/// which is particularly useful when you need to conditionally add tags or
/// configure the activity before execution.
/// </para>
/// <para>
/// The builder is immutable-style: each With* method returns the same builder
/// instance for chaining, but the final ExecuteAsync() creates and manages
/// the actual Activity.
/// </para>
/// </remarks>
public sealed class ActivityScopeBuilder
{
    private readonly ActivitySource _activitySource;
    private readonly string _activityName;
    private readonly List<(string Key, object? Value)> _tags = new();
    private ActivityKind _kind = ActivityKind.Internal;
    private ActivityContext _parent;
    private bool _hasParent;

    internal ActivityScopeBuilder(ActivitySource activitySource, string activityName)
    {
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _activityName = activityName ?? throw new ArgumentNullException(nameof(activityName));
    }

    /// <summary>
    /// Adds a tag that will be set when the activity is created.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public ActivityScopeBuilder WithTag(string key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _tags.Add((key, value));
        }
        return this;
    }

    /// <summary>
    /// Adds multiple tags that will be set when the activity is created.
    /// </summary>
    /// <param name="tags">The tags to add.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public ActivityScopeBuilder WithTags(params (string Key, object? Value)[] tags)
    {
        if (tags != null)
        {
            _tags.AddRange(tags);
        }
        return this;
    }

    /// <summary>
    /// Sets the ActivityKind for the activity.
    /// </summary>
    /// <param name="kind">The ActivityKind to use.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// ActivityKind describes the relationship between the activity and its parent/children:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Internal (default) - Activity within the same process</description></item>
    /// <item><description>Server - Activity handling a request</description></item>
    /// <item><description>Client - Activity making a request to another service</description></item>
    /// <item><description>Producer - Activity producing messages/events</description></item>
    /// <item><description>Consumer - Activity consuming messages/events</description></item>
    /// </list>
    /// </remarks>
    public ActivityScopeBuilder WithKind(ActivityKind kind)
    {
        _kind = kind;
        return this;
    }

    /// <summary>
    /// Sets an explicit parent context for the activity.
    /// </summary>
    /// <param name="parent">The parent ActivityContext.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// By default, activities are automatically linked to the current Activity.Current
    /// as their parent. Use this method when you need to explicitly set a different
    /// parent, such as when propagating trace context across async boundaries or
    /// from incoming HTTP headers.
    /// </para>
    /// </remarks>
    public ActivityScopeBuilder WithParent(ActivityContext parent)
    {
        _parent = parent;
        _hasParent = true;
        return this;
    }

    /// <summary>
    /// Sets an explicit parent activity.
    /// </summary>
    /// <param name="parent">The parent Activity (may be null).</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for WithParent(ActivityContext) that accepts
    /// an Activity instance. If the parent is null, the default parent linking behavior
    /// will be used.
    /// </para>
    /// </remarks>
    public ActivityScopeBuilder WithParent(Activity? parent)
    {
        if (parent != null)
        {
            _parent = parent.Context;
            _hasParent = true;
        }
        return this;
    }

    /// <summary>
    /// Executes an operation within the configured Activity scope.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This method creates the Activity with all configured settings, executes the
    /// operation, and automatically manages the activity lifecycle including:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Setting all configured tags</description></item>
    /// <item><description>Recording success (Ok status) on completion</description></item>
    /// <item><description>Recording errors (Error status + exception tags) on exception</description></item>
    /// <item><description>Disposing the activity when done</description></item>
    /// </list>
    /// </remarks>
    public async Task<T> ExecuteAsync<T>(Func<Activity?, Task<T>> operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = _hasParent
            ? _activitySource.StartActivity(_activityName, _kind, _parent)
            : _activitySource.StartActivity(_activityName, _kind);

        if (activity != null && _tags.Count > 0)
        {
            foreach (var (key, value) in _tags)
            {
                activity.SetTag(key, value);
            }
        }

        try
        {
            var result = await operation(activity).ConfigureAwait(false);
            activity.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a void operation within the configured Activity scope.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for operations that don't return a value.
    /// </para>
    /// </remarks>
    public async Task ExecuteAsync(Func<Activity?, Task> operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = _hasParent
            ? _activitySource.StartActivity(_activityName, _kind, _parent)
            : _activitySource.StartActivity(_activityName, _kind);

        if (activity != null && _tags.Count > 0)
        {
            foreach (var (key, value) in _tags)
            {
                activity.SetTag(key, value);
            }
        }

        try
        {
            await operation(activity).ConfigureAwait(false);
            activity.RecordSuccess();
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous operation within the configured Activity scope.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous overload useful for test scenarios and synchronous operations.
    /// For async operations, prefer the ExecuteAsync overloads.
    /// </para>
    /// </remarks>
    public T Execute<T>(Func<Activity?, T> operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = _hasParent
            ? _activitySource.StartActivity(_activityName, _kind, _parent)
            : _activitySource.StartActivity(_activityName, _kind);

        if (activity != null && _tags.Count > 0)
        {
            foreach (var (key, value) in _tags)
            {
                activity.SetTag(key, value);
            }
        }

        try
        {
            var result = operation(activity);
            activity.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous void operation within the configured Activity scope.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous overload useful for test scenarios and synchronous operations.
    /// For async operations, prefer the ExecuteAsync overloads.
    /// </para>
    /// </remarks>
    public void Execute(Action<Activity?> operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        using var activity = _hasParent
            ? _activitySource.StartActivity(_activityName, _kind, _parent)
            : _activitySource.StartActivity(_activityName, _kind);

        if (activity != null && _tags.Count > 0)
        {
            foreach (var (key, value) in _tags)
            {
                activity.SetTag(key, value);
            }
        }

        try
        {
            operation(activity);
            activity.RecordSuccess();
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
            throw;
        }
    }
}
