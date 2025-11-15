// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Extension methods for registering SRE services with dependency injection.
/// </summary>
public static class SreServiceCollectionExtensions
{
    /// <summary>
    /// Adds Site Reliability Engineering (SRE) services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Registers the following services:
    /// - ISliMetrics: Service for measuring and recording Service Level Indicators
    /// - IErrorBudgetTracker: Service for tracking error budgets
    /// - SloEvaluator: Background service for SLO evaluation
    ///
    /// These services are only active when SRE__ENABLED=true in configuration.
    /// </remarks>
    public static IServiceCollection AddSreServices(this IServiceCollection services)
    {
        // Register SLI metrics as singleton (maintains in-memory measurement queue)
        services.TryAddSingleton<ISliMetrics, SliMetrics>();

        // Register error budget tracker as singleton
        services.TryAddSingleton<IErrorBudgetTracker, ErrorBudgetTracker>();

        // Register SLO evaluator background service
        services.AddHostedService<SloEvaluator>();

        return services;
    }
}
