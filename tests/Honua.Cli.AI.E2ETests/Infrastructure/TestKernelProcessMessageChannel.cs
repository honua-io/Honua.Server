using System.Collections.Generic;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="IKernelProcessMessageChannel"/> for verifying emitted events in tests.
/// </summary>
internal sealed class TestKernelProcessMessageChannel : IKernelProcessMessageChannel
{
    public List<KernelProcessEvent> Events { get; } = new();

    public ValueTask EmitEventAsync(KernelProcessEvent processEvent)
    {
        Events.Add(processEvent);
        return ValueTask.CompletedTask;
    }
}
