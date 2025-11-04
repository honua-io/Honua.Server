using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Tests;

/// <summary>
/// Extension methods for tests.
/// </summary>
internal static class TestExtensions
{
    /// <summary>
    /// Converts an IEnumerable to IAsyncEnumerable for testing bulk operations.
    /// </summary>
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this IEnumerable<T> source,
        [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
        await Task.CompletedTask;
    }
}
