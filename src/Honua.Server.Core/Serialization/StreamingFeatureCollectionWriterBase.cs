// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Abstract base class for streaming feature collection writers.
/// Implements the Template Method pattern to eliminate duplication across format-specific writers.
/// Provides common streaming infrastructure including:
/// - Header/footer framing
/// - Feature iteration with cancellation support
/// - Periodic flushing for true streaming behavior
/// - Error handling with partial response support
/// - Performance telemetry and logging
/// </summary>
public abstract class StreamingFeatureCollectionWriterBase
{
    protected readonly ILogger _logger;

    /// <summary>
    /// Default batch size for periodic flushing (features processed before flush).
    /// </summary>
    protected virtual int FlushBatchSize => 100;

    /// <summary>
    /// Format-specific content type (e.g., "application/geo+json").
    /// </summary>
    protected abstract string ContentType { get; }

    /// <summary>
    /// Format name for logging and telemetry (e.g., "GeoJSON", "CSV").
    /// </summary>
    protected abstract string FormatName { get; }

    protected StreamingFeatureCollectionWriterBase(ILogger logger)
    {
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Template method for writing a complete feature collection to a stream.
    /// Orchestrates the streaming workflow:
    /// 1. Write header/opening
    /// 2. Stream features one at a time with periodic flushing
    /// 3. Write footer/closing
    /// 4. Handle errors and cancellation
    /// 5. Record telemetry
    /// </summary>
    public async Task WriteCollectionAsync(
        Stream outputStream,
        IAsyncEnumerable<FeatureRecord> features,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(features);
        Guard.NotNull(layer);
        Guard.NotNull(context);

        var sw = Stopwatch.StartNew();
        long featuresWritten = 0;
        long bytesWritten = 0;
        bool headerWritten = false;

        using var activity = HonuaTelemetry.OgcProtocols.StartActivity($"Streaming {FormatName} Write");
        activity?.SetTag("format", FormatName);
        activity?.SetTag("layer_id", layer.Id);

        try
        {
            // Write collection header/opening
            await WriteHeaderAsync(outputStream, layer, context, cancellationToken).ConfigureAwait(false);
            headerWritten = true;

            // Stream features
            bool isFirst = true;
            await foreach (var feature in features.WithCancellation(cancellationToken))
            {
                // Write separator (e.g., comma between JSON array elements)
                await WriteFeatureSeparatorAsync(outputStream, isFirst, cancellationToken).ConfigureAwait(false);

                // Write feature content
                await WriteFeatureAsync(outputStream, feature, layer, context, cancellationToken).ConfigureAwait(false);

                featuresWritten++;
                isFirst = false;

                // Flush periodically to enable true streaming and reduce memory pressure
                if (featuresWritten % FlushBatchSize == 0)
                {
                    await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    // Allow cancellation and backpressure
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Write collection footer/closing
            await WriteFooterAsync(outputStream, layer, context, featuresWritten, cancellationToken).ConfigureAwait(false);

            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Calculate bytes written if possible
            if (outputStream.CanSeek)
            {
                bytesWritten = outputStream.Position;
            }

            sw.Stop();

            // Record telemetry
            activity?.SetTag("features_written", featuresWritten);
            activity?.SetTag("bytes_written", bytesWritten);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("streaming", true);

            LogCompletion(featuresWritten, sw.ElapsedMilliseconds, bytesWritten, layer.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Streaming {Format} write cancelled after {Features} features - Layer: {LayerId}",
                FormatName, featuresWritten, layer.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Streaming {Format} write failed after {Features} features - Layer: {LayerId}",
                FormatName, featuresWritten, layer.Id);

            // If we haven't written the header yet, we can rethrow and let the caller send a proper error
            // If we've already started streaming, the response is partial and client will see truncated output
            if (!headerWritten)
            {
                throw;
            }

            _logger.LogWarning(
                "Cannot send error response - already wrote {Features} features to stream. Client will receive truncated {Format}.",
                featuresWritten, FormatName);

            // Don't rethrow - response already started, allow graceful termination
        }
    }

    /// <summary>
    /// Writes the collection header/opening (e.g., opening brace and "features": [ for GeoJSON).
    /// Called once before any features are written.
    /// </summary>
    protected abstract Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the feature separator (e.g., comma between JSON array elements, newline for CSV/ndjson).
    /// Called before each feature except the first.
    /// </summary>
    /// <param name="outputStream">The output stream to write to.</param>
    /// <param name="isFirst">True if this is the first feature (no separator needed)</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    protected abstract Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes a single feature to the stream.
    /// Called once for each feature in the collection.
    /// </summary>
    /// <param name="outputStream">The output stream to write to.</param>
    /// <param name="feature">The feature to write.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="context">The streaming writer context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    protected abstract Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the collection footer/closing (e.g., closing array and object for GeoJSON).
    /// Called once after all features have been written.
    /// </summary>
    /// <param name="outputStream">The output stream to write to.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="context">The streaming writer context.</param>
    /// <param name="featuresWritten">Total number of features written</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    protected abstract Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken);

    /// <summary>
    /// Logs completion metrics. Can be overridden for custom logging behavior.
    /// </summary>
    protected virtual void LogCompletion(long featuresWritten, long durationMs, long bytesWritten, string layerId)
    {
        if (durationMs > 1000)
        {
            _logger.LogWarning(
                "Slow streaming {Format} write: {Duration}ms for {Features} features ({Bytes} bytes) - Layer: {LayerId}",
                FormatName, durationMs, featuresWritten, bytesWritten, layerId);
        }
        else
        {
            _logger.LogInformation(
                "Streaming {Format} write completed: {Features} features in {Duration}ms ({Bytes} bytes)",
                FormatName, featuresWritten, durationMs, bytesWritten);
        }
    }
}

/// <summary>
/// Context object for streaming writer operations.
/// Encapsulates format-specific options and metadata.
/// </summary>
public sealed class StreamingWriterContext
{
    /// <summary>
    /// Target coordinate reference system WKID (e.g., 4326 for WGS84).
    /// </summary>
    public int TargetWkid { get; init; } = 4326;

    /// <summary>
    /// Whether to include geometry in output.
    /// </summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>
    /// Maximum allowable offset for geometry simplification (map units).
    /// </summary>
    public double? MaxAllowableOffset { get; init; }

    /// <summary>
    /// Number of decimal places for coordinate precision.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Whether to pretty-print JSON output.
    /// </summary>
    public bool PrettyPrint { get; init; }

    /// <summary>
    /// Total count of matching features (for pagination metadata).
    /// </summary>
    public long? TotalCount { get; init; }

    /// <summary>
    /// Anticipated number of features that will be returned in this response.
    /// Used by writers that need to emit metadata before streaming begins.
    /// </summary>
    public long? ExpectedFeatureCount { get; init; }

    /// <summary>
    /// Query limit for pagination.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Query offset for pagination.
    /// </summary>
    public long? Offset { get; init; }

    /// <summary>
    /// Service identifier for telemetry.
    /// </summary>
    public string? ServiceId { get; init; }

    /// <summary>
    /// Selected property names to include in output (null means all properties).
    /// Used for field filtering to prevent data leakage and reduce payload size.
    /// </summary>
    public IReadOnlyList<string>? PropertyNames { get; init; }

    /// <summary>
    /// Format-specific options (e.g., CSV delimiter, geometry format).
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Options { get; init; }

    /// <summary>
    /// Gets a typed option value.
    /// </summary>
    public T? GetOption<T>(string key, T? defaultValue = default)
    {
        if (Options == null || !Options.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
