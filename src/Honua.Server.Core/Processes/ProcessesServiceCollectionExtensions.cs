// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Processes.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Extension methods for registering OGC API - Processes services.
/// </summary>
public static class ProcessesServiceCollectionExtensions
{
    /// <summary>
    /// Adds OGC API - Processes support to the service collection.
    /// </summary>
    public static IServiceCollection AddOgcProcesses(this IServiceCollection services)
    {
        // Register core services
        services.AddSingleton<IProcessRegistry, ProcessRegistry>();
        services.AddSingleton<ProcessJobStore>();
        services.AddSingleton<CompletedProcessJobStore>();
        services.AddSingleton<ProcessExecutionService>();
        services.AddHostedService(sp => sp.GetRequiredService<ProcessExecutionService>());

        // Register built-in processes
        services.AddSingleton<IProcess, BufferProcess>();
        services.AddSingleton<IProcess, CentroidProcess>();
        services.AddSingleton<IProcess, DissolveProcess>();
        services.AddSingleton<IProcess, ClipProcess>();
        services.AddSingleton<IProcess, ReprojectProcess>();

        // Register all IProcess implementations with the registry
        services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<IProcessRegistry>();
            var processes = sp.GetServices<IProcess>();

            foreach (var process in processes)
            {
                registry.RegisterProcess(process);
            }

            return registry;
        });

        return services;
    }
}
