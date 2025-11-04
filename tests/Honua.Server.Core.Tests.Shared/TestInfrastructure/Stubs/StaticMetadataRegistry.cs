using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Static implementation of IMetadataRegistry that returns a fixed snapshot.
/// Useful for testing scenarios where metadata doesn't change during the test.
/// </summary>
/// <remarks>
/// This implementation:
/// - Always returns the same snapshot provided at construction
/// - Reports as initialized immediately
/// - Ignores reload and update requests
/// - Returns a no-op change token that never signals changes
/// </remarks>
public sealed class StaticMetadataRegistry : IMetadataRegistry
{
    /// <summary>
    /// Initializes a new instance with the specified snapshot.
    /// </summary>
    /// <param name="snapshot">The metadata snapshot to return for all queries.</param>
    public StaticMetadataRegistry(MetadataSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    /// <summary>
    /// Gets the metadata snapshot.
    /// </summary>
    public MetadataSnapshot Snapshot { get; }

    /// <summary>
    /// Always returns true since the snapshot is available immediately.
    /// </summary>
    public bool IsInitialized => true;

    /// <summary>
    /// Returns the configured snapshot.
    /// </summary>
    public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Snapshot);

    /// <summary>
    /// No-op since the registry is already initialized.
    /// </summary>
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// No-op since the registry uses a static snapshot.
    /// </summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Returns a no-op change token that never signals changes.
    /// </summary>
    public IChangeToken GetChangeToken() => TestChangeTokens.Noop;

    /// <summary>
    /// No-op since the registry uses a static snapshot.
    /// </summary>
    public void Update(MetadataSnapshot snapshot) { }

    /// <summary>
    /// No-op since the registry uses a static snapshot.
    /// </summary>
    public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Always succeeds and returns the configured snapshot.
    /// </summary>
    public bool TryGetSnapshot(out MetadataSnapshot snapshot)
    {
        snapshot = Snapshot;
        return true;
    }
}
