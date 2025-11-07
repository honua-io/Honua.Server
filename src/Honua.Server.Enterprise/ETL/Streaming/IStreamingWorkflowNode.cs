// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using NetTopologySuite.Features;

namespace Honua.Server.Enterprise.ETL.Streaming;

/// <summary>
/// Interface for workflow nodes that support streaming data processing
/// </summary>
public interface IStreamingWorkflowNode
{
    /// <summary>
    /// Processes features as a stream (one at a time or in micro-batches)
    /// </summary>
    IAsyncEnumerable<IFeature> ProcessStreamAsync(
        IAsyncEnumerable<IFeature> inputFeatures,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Streaming context for nodes that need additional state
/// </summary>
public class StreamingContext
{
    /// <summary>
    /// Total features processed so far
    /// </summary>
    public long FeaturesProcessed { get; set; }

    /// <summary>
    /// Shared state across stream processing
    /// </summary>
    public Dictionary<string, object> State { get; set; } = new();

    /// <summary>
    /// Progress callback
    /// </summary>
    public IProgress<StreamProgress>? ProgressCallback { get; set; }
}

/// <summary>
/// Progress information for stream processing
/// </summary>
public class StreamProgress
{
    /// <summary>
    /// Features processed
    /// </summary>
    public long FeaturesProcessed { get; set; }

    /// <summary>
    /// Current throughput (features/second)
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Progress message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Bytes read
    /// </summary>
    public long? BytesRead { get; set; }

    /// <summary>
    /// Bytes written
    /// </summary>
    public long? BytesWritten { get; set; }
}

/// <summary>
/// Helper extensions for streaming workflows
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Batches features into micro-batches for efficient processing
    /// </summary>
    public static async IAsyncEnumerable<List<IFeature>> BatchAsync(
        this IAsyncEnumerable<IFeature> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var batch = new List<IFeature>(batchSize);

        await foreach (var feature in source.WithCancellation(cancellationToken))
        {
            batch.Add(feature);

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<IFeature>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Flattens batches back into individual features
    /// </summary>
    public static async IAsyncEnumerable<IFeature> UnbatchAsync(
        this IAsyncEnumerable<List<IFeature>> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in source.WithCancellation(cancellationToken))
        {
            foreach (var feature in batch)
            {
                yield return feature;
            }
        }
    }

    /// <summary>
    /// Counts features in a stream without consuming them
    /// </summary>
    public static async IAsyncEnumerable<IFeature> CountAsync(
        this IAsyncEnumerable<IFeature> source,
        IProgress<long> counter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long count = 0;

        await foreach (var feature in source.WithCancellation(cancellationToken))
        {
            count++;
            counter.Report(count);
            yield return feature;
        }
    }
}
