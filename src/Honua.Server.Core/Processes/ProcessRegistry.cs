// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Default implementation of the process registry.
/// </summary>
internal sealed class ProcessRegistry : IProcessRegistry
{
    private readonly ConcurrentDictionary<string, IProcess> _processes = new();

    public IReadOnlyList<ProcessDescription> GetAllProcesses()
    {
        return _processes.Values
            .Select(p => p.Description)
            .OrderBy(p => p.Id)
            .ToList();
    }

    public IProcess? GetProcess(string processId)
    {
        _processes.TryGetValue(processId, out var process);
        return process;
    }

    public void RegisterProcess(IProcess process)
    {
        _processes[process.Description.Id] = process;
    }
}
